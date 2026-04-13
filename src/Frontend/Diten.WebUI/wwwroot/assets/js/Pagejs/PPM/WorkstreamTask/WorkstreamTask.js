'use strict';
const userName = window.getUserName();
let dt_workstream_task_table;
let dtWorkstreamTask = null;
let currentEditingTaskId = "";
const categoryIconMap = {
    1: { icon: 'bx bx-calendar', color: 'primary' },   // Planning
    2: { icon: 'bx bx-play', color: 'success' },       // Execution
    3: { icon: 'bx bx-search-alt', color: 'info' },    // Review
    4: { icon: 'bx bx-refresh', color: 'warning' },   // Follow-up
    5: { icon: 'bx bx-check-shield', color: 'dark' },  // Decision
    6: { icon: 'bx bx-chat', color: 'secondary' },     // Discussion
    7: { icon: 'bx bx-file', color: 'primary' },       // Reporting
    8: { icon: 'bx bx-check-circle', color: 'success' }, // Approval
    9: { icon: 'bx bx-error', color: 'danger' },       // Risk / Issue
    10: { icon: 'bx bx-dots-horizontal-rounded', color: 'secondary' } // Other
};
let dependencyReasonMap = {};
const dependencyTypeInfo = {
    1: { code: 'FS', label: 'Finish to Start', description: 'This task cannot start until the selected task is finished.' },
    2: { code: 'SS', label: 'Start to Start', description: 'This task cannot start until the selected task has started.' },
    3: { code: 'FF', label: 'Finish to Finish', description: 'This task cannot finish until the selected task is finished.' },
    4: { code: 'SF', label: 'Start to Finish', description: 'This task cannot finish until the selected task has started.' }
};

function getDependencySvg(type, direction) {
    const colors = { task: '#696cff', arrow: '#8592a3' }; // Primary & Secondary
    let markers = `<defs><marker id="arrowhead" markerWidth="10" markerHeight="7" refX="0" refY="3.5" orient="auto"><polygon points="0 0, 10 3.5, 0 7" fill="${colors.arrow}" /></marker></defs>`;
    let path = '';
    const isBlockedBy = direction !== 'blocks';
    const typeCode = dependencyTypeInfo[type]?.code || type;

    if (isBlockedBy) {
        if (typeCode === 'FS') path = `<line x1="50" y1="25" x2="102" y2="25" stroke="${colors.arrow}" stroke-width="2" marker-end="url(#arrowhead)" />`;
        if (typeCode === 'SS') path = `<path d="M 30 35 L 30 42 L 130 42 L 130 35" fill="none" stroke="${colors.arrow}" stroke-width="2" marker-end="url(#arrowhead)" />`;
        if (typeCode === 'FF') path = `<path d="M 50 25 L 65 25 L 65 10 L 150 10 L 150 15" fill="none" stroke="${colors.arrow}" stroke-width="2" marker-end="url(#arrowhead)" />`;
        if (typeCode === 'SF') path = `<path d="M 30 35 L 30 42 L 90 42 L 90 25 L 102 25" fill="none" stroke="${colors.arrow}" stroke-width="2" marker-end="url(#arrowhead)" />`;
    } else {
        if (typeCode === 'FS') path = `<line x1="102" y1="25" x2="50" y2="25" stroke="${colors.arrow}" stroke-width="2" marker-end="url(#arrowhead)" />`;
        if (typeCode === 'SS') path = `<path d="M 130 35 L 130 42 L 30 42 L 30 35" fill="none" stroke="${colors.arrow}" stroke-width="2" marker-end="url(#arrowhead)" />`;
        if (typeCode === 'FF') path = `<path d="M 150 15 L 150 10 L 65 10 L 65 25 L 50 25" fill="none" stroke="${colors.arrow}" stroke-width="2" marker-end="url(#arrowhead)" />`;
        if (typeCode === 'SF') path = `<path d="M 102 25 L 90 25 L 90 42 L 30 42 L 30 35" fill="none" stroke="${colors.arrow}" stroke-width="2" marker-end="url(#arrowhead)" />`;
    }

    const box1 = `<rect x="10" y="15" width="40" height="20" rx="4" fill="${colors.task}" opacity="0.2" /> <text x="30" y="28" font-size="6" text-anchor="middle" fill="${colors.task}">Selected</text>`;
    const box2 = `<rect x="110" y="15" width="40" height="20" rx="4" fill="${colors.task}" opacity="0.8" /> <text x="130" y="28" font-size="6" text-anchor="middle" fill="#fff">This Task</text>`;
    return `<svg width="160" height="50" viewBox="0 0 160 50" xmlns="http://www.w3.org/2000/svg">${markers}${box1}${box2}${path}</svg>`;
}
const urlParams = new URLSearchParams(window.location.search);
const pageMode = urlParams.get('mode') || 'create';
const workStreamId = window.location.pathname.split('/')[2];
function htmlToPlainText(html) {
    if (!html) return '';

    const temp = document.createElement('div');
    temp.innerHTML = html;

    // img, video, iframe tamamen kaldır
    temp.querySelectorAll('img, video, iframe').forEach(el => el.remove());

    // Quill UI elementlerini kaldır
    temp.querySelectorAll('.ql-ui').forEach(el => el.remove());

    // Liste item'ları düzgün boşlukla ayır
    temp.querySelectorAll('li').forEach(li => {
        li.prepend('• ');
    });

    return temp.textContent || temp.innerText || '';
}
function smartTruncate(text, maxLength = 80) {
    if (!text) return '-';

    const clean = text.replace(/\s+/g, ' ').trim();

    if (clean.length <= maxLength) return clean;

    // kelimeyi yarım kesme
    return clean.slice(0, maxLength).replace(/\s+\S*$/, '') + '…';
}

function minutesToHour(min) {
    if (!min) return '0h';
    const hours = Math.floor(min / 60);
    const minutes = min % 60;
    if (minutes === 0) return `${hours}h`;
    return `${(min / 60).toFixed(1)}h`.replace('.0', '');
}

function renderProgress(row) {
    const total = row.totalSubTaskCount || 0;
    const completed = row.completedSubTaskCount || 0;
    const progress = total > 0 ? Math.round((completed / total) * 100) : 0;

    return `
        <div class="d-flex align-items-center gap-3">
            <p class="fw-medium mb-0 text-heading" style="min-width:40px;">${progress}%</p>
            <div class="progress w-100" style="height:8px;">
                <div class="progress-bar" style="width:${progress}%" aria-valuenow="${progress}" aria-valuemin="0" aria-valuemax="100"></div>
            </div>
            <small class="text-nowrap">${completed} / ${total}</small>
        </div>
    `;
}

function renderEstimated(row) {
    const ownMin = row.ownEstimatedMinute || 0;
    const subMin = row.subTasksTotalEstimatedMinute || 0;
    const ownStr = minutesToHour(ownMin);
    const subStr = subMin > 0 ? minutesToHour(subMin) : '-';

    return `
        <div class="d-flex flex-column text-nowrap">
            <small class="fw-medium">Own: ${ownStr}</small>
            <small class="text-muted">Sub: ${subStr}</small>
        </div>
    `;
}

function updateWorkstreamTaskStats(tasks) {
    if (!Array.isArray(tasks)) return;

    const today = moment().startOf('day');

    const total = tasks.length;
    const completed = tasks.filter(t => t.statusId === 3).length;
    const overdue = tasks.filter(t =>
        t.statusId !== 3 &&
        t.endDate &&
        moment(t.endDate).isBefore(today)
    ).length;
    const critical = tasks.filter(t => t.priorityId === 4).length;

    const totalEl = document.getElementById('ws-total-task');
    const completedEl = document.getElementById('ws-completed-task');
    const overdueEl = document.getElementById('ws-overdue-task');
    const criticalEl = document.getElementById('ws-critical-task');

    if (totalEl) totalEl.textContent = total;
    if (completedEl) completedEl.textContent = completed;
    if (overdueEl) overdueEl.textContent = overdue;
    if (criticalEl) criticalEl.textContent = critical;
}
function showToast(message, type = 'success') {
    const toastEl = document.getElementById('appToast');
    const toastBody = toastEl.querySelector('.toast-body');
    const toastHeader = toastEl.querySelector('#appToastHeader');

    if (!toastEl || !toastBody || !toastHeader) return;

    toastBody.innerHTML = message;

    toastEl.classList.remove('bg-success', 'bg-danger', 'bg-warning', 'bg-info');

    switch (type) {
        case 'success':
            toastEl.classList.add('bg-success');
            toastHeader.textContent = 'Successfull';
            break;
        case 'error':
            toastEl.classList.add('bg-danger');
            toastHeader.textContent = 'Error';
            break;
        case 'warning':
            toastEl.classList.add('bg-warning');
            toastHeader.textContent = 'Warning';
            break;
        case 'info':
            toastEl.classList.add('bg-info');
            toastHeader.textContent = 'Information';
            break;
    }

    const toast = bootstrap.Toast.getOrCreateInstance(toastEl);
    toast.show();
}

function initWorkstreamTaskTable() {

    const tableSelector = '.workstream-task-table';
    const apiUrl = `${API.ppm}/Workstream/GetTaskHierarchyByParentTask/${workStreamId}`;
    //const workflowId = selectedWorkflowId; // sayfa context’inden geliyor
    //let tableTitle = document.createElement('h5');
    //tableTitle.classList.add('card-title', 'mb-0', 'text-nowrap', 'text-md-start', 'text-center');
    //tableTitle.innerHTML = 'Task Overview';
    const tableEl = document.querySelector(tableSelector);
    if (!tableEl) return;

    // tekrar init edilmesin
    if (dtWorkstreamTask) {
        dtWorkstreamTask.destroy();
        dtWorkstreamTask = null;
    }

    dtWorkstreamTask = new DataTable(tableEl, {
        destroy: true,
        stateSave: false,
        pageLength: 100,
        lengthMenu: [10, 25, 50, 100],
        ajax: {
            url: apiUrl,
            type: 'GET',
            dataSrc: function (json) {
                updateWorkstreamTaskStats(json.data);
                return json.data;
            }
        },

        columns: [
            { data: null },           // responsive control
            { data: 'name' },         // Name
            { data: 'categoryName' }, // Category
            { data: 'assignees' },    // Assignee
            { data: null },           // Progress
            { data: null },           // Priority
            { data: null },           // Date
            { data: 'statusName' },   // Status
            { data: null },
            { data: null },
            { data: null },
            { data: null }         // Action
        ],

        columnDefs: [
            // Responsive control
            {
                className: 'control',
                orderable: false,
                searchable: false,
                targets: 0,
                render: () => ''
            },

            // Name
            {
                targets: 1, // Name column
                responsivePriority: 1,
                responsive: false,
                render: (data, type, row) => {

                    const category = categoryIconMap[row.categoryId] || {};
                    const icon = category.icon || 'bx bx-task';
                    const color = category.color || 'secondary';
                    const shortDescription = htmlToPlainText(row.description);
                    // OWNER avatar (baş harf)
                    let ownerHtml = '';
                    let ownerSearchText = '';

                    if (row.ownerName) {
                        const fullName = row.ownerName;
                        ownerSearchText = fullName;

                        const initials = fullName
                            .split(' ')
                            .map(x => x[0]?.toUpperCase())
                            .join('')
                            .slice(0, 2);

                        ownerHtml = `
            <span class="badge bg-label-primary rounded-circle
                         d-inline-flex align-items-center justify-content-center me-2"
                  style="width:24px; height:24px; font-size:10px;"
                  data-bs-toggle="tooltip"
                  title="${fullName}">
                ${initials}
            </span>
        `;
                    } else {
                        ownerHtml = `
            <span class="badge bg-label-secondary rounded-circle
                         d-inline-flex align-items-center justify-content-center me-2"
                  style="width:24px; height:24px;">
                -
            </span>
        `;
                    }

                    // 🔍 Search text (owner dahil)
                    const searchText = `
        ${row.name}
        ${shortDescription}
        ${row.ownerName || ''}
    `;

                    return `
                    <span class="d-none">${row.ownerName}</span>
        <div class="d-flex align-items-center" data-search="${searchText}">
            
            <!-- ICON -->
            <span class="me-4">
                <span class="badge bg-label-${color} rounded p-1_5">
                    <i class="icon-base bx ${icon}"></i>
                </span>
            </span>

            <!-- CONTENT -->
            <div>
                <!-- Task Name -->
                <a href="javascript:void(0);"
                   class="text-heading fw-medium text-truncate text-wrap workstream-task-name"
                   data-task-id="${row.id}">
                    ${row.name}
                </a>

                <!-- OWNER (ACADEMY STYLE) -->
                <div class="d-flex align-items-center mt-1">
                    <div class="avatar-wrapper me-2">
                        ${ownerHtml}
                    </div>
                    <small class="text-heading truncate-2" title="${shortDescription}">
                        ${shortDescription}
                    </small>
                </div>
            </div>
        </div>
    `;
                }
            },
            {
                targets: 2, // Category column index
                responsivePriority: 3,
                render: (data, type, row) => {

                    const category = categoryIconMap[row.categoryId] || {};
                    const color = category.color || 'secondary';

                    return `
            <span class="badge bg-label-${color} category-badge-wrap"
                  title="${row.categoryName}">
                ${row.categoryName}
            </span>
        `;
                }
            },


            // Assignees (placeholder)
            {
                targets: 3, // Assignees column
                orderable: true,
                responsivePriority: 1,
                render: function (data, type, row) {

                    // Assignee listesi
                    const assignees = Array.isArray(row.assignees) ? row.assignees : [];

                    // 🔍 SEARCH & SORT için (EN KRİTİK KISIM)
                    if (type === 'filter' || type === 'sort') {
                        // "Beste Pullukçu Ali Yılmaz" gibi düz text döner
                        return assignees.map(a => a.name).join(' ');
                    }

                    // DISPLAY için HTML
                    if (assignees.length === 0) {
                        return `
                <span class="badge bg-label-secondary rounded-circle
                             d-inline-flex align-items-center justify-content-center"
                      style="width:32px; height:32px; font-size:12px;">
                    -
                </span>
            `;
                    }

                    const colors = ["primary", "success", "warning", "danger", "info", "dark"];

                    return assignees.map((a, i) => {
                        const fullName = a.name || "";

                        const initials = fullName
                            .split(" ")
                            .map(x => x[0]?.toUpperCase())
                            .join("")
                            .slice(0, 2);

                        const color = colors[i % colors.length];

                        return `
                <span class="badge bg-label-${color} rounded-circle
                             d-inline-flex align-items-center justify-content-center me-1"
                      style="width:32px; height:32px; font-size:12px;"
                      data-bs-toggle="tooltip"
                      data-bs-offset="0,8"
                      data-bs-placement="top"
                      data-bs-custom-class="tooltip-primary"
                      title="${fullName}">
                    ${initials}
                </span>
            `;
                    }).join('');
                }
            },


            // Progress
            {
                targets: 4,
                responsivePriority: 1,
                orderable: true,
                render: function (data, type, row) {
                    const total = row.totalSubTaskCount || 0;
                    const completed = row.completedSubTaskCount || 0;
                    const progress = total > 0 ? Math.round((completed / total) * 100) : 0;

                    if (type === 'filter' || type === 'sort') {
                        return `${completed}/${total} ${progress}%`;
                    }
                    return renderProgress(row);
                }
            },
            {
                targets: 5,
                responsivePriority: 1,
                render: function (data, type, full) {
                    const priorityId = Number(full.priorityId);
                    const priorityName = full.priorityName || '';
                    if (type === 'filter' || type === 'sort') return priorityName;
                    const colors = { 1: 'primary', 2: 'info', 3: 'warning', 4: 'danger' };
                    return `<span class="badge bg-label-${colors[priorityId] || 'secondary'} text-nowrap">${priorityName || '-'}</span>`;
                }
            },
            {
                targets: 6, // Date column
                render: (data, type, full) => {
                    const start = full.startDate ? moment(full.startDate).format('DD MMM YY') : '-';
                    const end = full.endDate ? moment(full.endDate).format('DD MMM YY') : '-';
                    if (type === 'filter' || type === 'sort') return `${start} ${end}`;

                    const isCompleted = full.statusId === 3;
                    let endColorClass = 'text-primary';
                    if (full.endDate && !isCompleted) {
                        const diffDays = moment(full.endDate).diff(moment().startOf('day'), 'days');
                        if (diffDays < 0) endColorClass = 'text-danger';
                        else if (diffDays <= 3) endColorClass = 'text-warning';
                    }
                    if (isCompleted) endColorClass = 'text-success';

                    return `<div class="d-flex flex-column text-nowrap"><small class="text-muted">${start}</small><small class="fw-medium ${endColorClass}">${end}</small></div>`;
                }
            },

            {
                targets: 7, // Status column
                responsivePriority: 1,
                render: function (data, type, full) {
                    if (type === 'filter' || type === 'sort') return full.statusName || '';
                    const colors = { 3: 'success', 2: 'primary', 1: 'warning', 4: 'danger' };
                    return `<span class="badge bg-${colors[full.statusId] || 'secondary'} text-nowrap">${full.statusName || '-'}</span>`;
                }
            },

            {
                targets: 8,
                render: function (data, type, row) {
                    const ownStr = minutesToHour(row.ownEstimatedMinute);
                    const subStr = row.subTasksTotalEstimatedMinute > 0 ? minutesToHour(row.subTasksTotalEstimatedMinute) : '-';
                    if (type === 'filter' || type === 'sort') return `Own ${ownStr} Sub ${subStr}`;
                    return renderEstimated(row);
                }
            },

            {
                targets: 9,
                render: function (data, type, row) {
                    const total = row.totalSubTaskCount || 0;
                    const completed = row.completedSubTaskCount || 0;
                    if (type === 'filter' || type === 'sort') return total > 0 ? `${completed} ${total}` : '';
                    return total > 0 ? `${completed} / ${total}` : `<span class="badge bg-label-secondary text-nowrap">No subtasks</span>`;
                }
            },

            {
                targets: 10, // Dependencies column
                responsivePriority: 2,
                render: function (data, type, row) {
                    const deps = Array.isArray(row.dependencies) ? row.dependencies : [];

                    if (type === 'filter' || type === 'sort') {
                        return deps.map(d => `${d.direction} ${d.dependencyTaskName}`).join(' ');
                    }

                    if (deps.length === 0) {
                        return '<span class="badge bg-label-secondary text-nowrap">No dependency</span>';
                    }

                    return `
                        <div class="d-flex align-items-center gap-1">
                            ${deps.map(d => `
                                <span class="${d.direction === 'blocked_by' ? 'text-danger' : 'text-info'}"
                                      data-bs-toggle="tooltip"
                                      title="${d.direction === 'blocked_by' ? 'Blocked by' : 'Blocks'}: ${d.dependencyTaskName} (${d.dependencyTypeName})">
                                    <i class="bx ${d.direction === 'blocked_by' ? 'bx-block' : 'bx-link'}"></i>
                                </span>
                            `).join('')}
                            <span class="badge bg-label-${deps.some(d => d.direction === 'blocked_by') ? 'danger' : 'info'}">
                                ${deps.length}
                            </span>
                        </div>
                    `;
                }
            },

            {
                targets: 11,
                responsivePriority: 0,
                className: 'dt-action-col',
                orderable: false,
                searchable: false,
                render: (data, type, row) => `
                    <div class="dropdown">
                        <button class="btn btn-icon" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded"></i></button>
                        <div class="dropdown-menu dropdown-menu-end">
                            <a class="dropdown-item d-flex alig-items-center view-record" href="javascript:;" data-id="${row.id}"><i class="icon-sm bx bx-show me-2"></i>View</a>
                            <a class="dropdown-item edit-record d-flex alig-items-center" href="javascript:;" data-id="${row.id}"><i class="icon-sm bx bx-edit-alt me-2"></i> Edit</a>
                            <a class="dropdown-item open-record d-flex alig-items-center" href="#"><i class="icon-sm bx bx-door-open me-2"></i> Open</a>
                            <a class="dropdown-item delete-record d-flex alig-items-center text-danger" href="#"><i class="icon-sm bx bx-trash me-2"></i> Delete</a>
                        </div>
                    </div>`
            }
        ],

        select: {
            style: 'multi',
            selector: 'td:nth-child(2)'
        },
        order: [[2, 'desc']],
        layout: {
            topStart: {
                rowClass: 'row mx-3 my-0 justify-content-between',
                features: [
                    {
                        pageLength: {
                            menu: [10, 25, 50, 100],
                            text: '_MENU_'
                        }
                    }
                ]
            },
            topEnd: {
                features: [
                    {
                        search: {
                            placeholder: 'Search',
                            text: '_INPUT_'
                        }
                    },
                    {
                        buttons: [
                            {
                                extend: 'collection',
                                className: 'btn btn-label-secondary dropdown-toggle',
                                text: '<span class="d-flex align-items-center gap-2"><i class="icon-base bx bx-export icon-sm"></i> <span class="d-none d-sm-inline-block">Export</span></span>',
                                buttons: [
                                    {
                                        extend: 'print',
                                        text: `<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-2"></i>Print</span>`,
                                        className: 'dropdown-item',
                                        exportOptions: {
                                            columns: ':visible:not(.dt-action-col)',
                                            format: {
                                                body: function (inner, colIdx, rowIdx) {
                                                    if (!inner) return '';
                                                    // Progress check
                                                    if (inner.includes('%') && inner.includes('/')) {
                                                        return inner.replace(/\s+/g, ' ').trim();
                                                    }
                                                    // Dependencies check
                                                    if (inner.includes('bx-block') || inner.includes('bx-link')) {
                                                        return inner.replace(/<[^>]*>/g, '').replace(/\s+/g, ' ').trim();
                                                    }
                                                    // Default HTML strip
                                                    return inner.replace(/<[^>]*>/g, '').trim();
                                                }
                                            }
                                        },
                                        customize: function (win) {
                                            win.document.body.style.color = config.colors.headingColor;
                                            win.document.body.style.borderColor = config.colors.borderColor;
                                            win.document.body.style.backgroundColor = config.colors.bodyBg;
                                            const table = win.document.body.querySelector('table');
                                            table.classList.add('compact');
                                            table.style.color = 'inherit';
                                            table.style.borderColor = 'inherit';
                                            table.style.backgroundColor = 'inherit';
                                        }
                                    },
                                    {
                                        extend: 'csv',
                                        text: `<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-2"></i>Csv</span>`,
                                        className: 'dropdown-item',
                                        exportOptions: {
                                            columns: [3, 4, 5, 6, 7],
                                            format: {
                                                body: function (inner, coldex, rowdex) {
                                                    if (inner.length <= 0) return inner;
                                                    const el = new DOMParser().parseFromString(inner, 'text/html').body.childNodes;
                                                    let result = '';
                                                    el.forEach(item => {
                                                        if (item.classList && item.classList.contains('user-name')) {
                                                            result += item.lastChild.firstChild.textContent;
                                                        } else {
                                                            result += item.textContent || item.innerText || '';
                                                        }
                                                    });
                                                    return result;
                                                }
                                            }
                                        }
                                    },
                                    {
                                        extend: 'excel',
                                        text: `<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-2"></i>Excel</span>`,
                                        className: 'dropdown-item',
                                        exportOptions: {
                                            columns: [3, 4, 5, 6, 7],
                                            format: {
                                                body: function (inner, coldex, rowdex) {
                                                    if (inner.length <= 0) return inner;
                                                    const el = new DOMParser().parseFromString(inner, 'text/html').body.childNodes;
                                                    let result = '';
                                                    el.forEach(item => {
                                                        if (item.classList && item.classList.contains('user-name')) {
                                                            result += item.lastChild.firstChild.textContent;
                                                        } else {
                                                            result += item.textContent || item.innerText || '';
                                                        }
                                                    });
                                                    return result;
                                                }
                                            }
                                        }
                                    },
                                    {
                                        extend: 'pdf',
                                        text: `<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-pdf me-2"></i>Pdf</span>`,
                                        className: 'dropdown-item',
                                        exportOptions: {
                                            columns: [3, 4, 5, 6, 7],
                                            format: {
                                                body: function (inner, coldex, rowdex) {
                                                    if (inner.length <= 0) return inner;
                                                    const el = new DOMParser().parseFromString(inner, 'text/html').body.childNodes;
                                                    let result = '';
                                                    el.forEach(item => {
                                                        if (item.classList && item.classList.contains('user-name')) {
                                                            result += item.lastChild.firstChild.textContent;
                                                        } else {
                                                            result += item.textContent || item.innerText || '';
                                                        }
                                                    });
                                                    return result;
                                                }
                                            }
                                        }
                                    },
                                    {
                                        extend: 'copy',
                                        text: `<i class="icon-base bx bx-copy me-1"></i>Copy`,
                                        className: 'dropdown-item',
                                        exportOptions: {
                                            columns: [3, 4, 5, 6, 7],
                                            format: {
                                                body: function (inner, coldex, rowdex) {
                                                    if (inner.length <= 0) return inner;
                                                    const el = new DOMParser().parseFromString(inner, 'text/html').body.childNodes;
                                                    let result = '';
                                                    el.forEach(item => {
                                                        if (item.classList && item.classList.contains('user-name')) {
                                                            result += item.lastChild.firstChild.textContent;
                                                        } else {
                                                            result += item.textContent || item.innerText || '';
                                                        }
                                                    });
                                                    return result;
                                                }
                                            }
                                        }
                                    }
                                ]
                            }

                        ]
                    }
                ]
            },
            bottomStart: {
                rowClass: 'row mx-3 justify-content-between',
                features: ['info']
            },
            bottomEnd: {
                paging: {
                    firstLast: false
                }
            }
        },
        language: {
            sLengthMenu: '_MENU_',
            search: '',
            searchPlaceholder: 'Search',
            paginate: {
                next: '<i class="icon-base bx bx-chevron-right icon-18px"></i>',
                previous: '<i class="icon-base bx bx-chevron-left icon-18px"></i>'
            }
        },
        // For responsive popup
        responsive: {
            details: {
                display: DataTable.Responsive.display.modal({
                    header: function (row) {
                        const data = row.data();
                        return 'Details of ' + data['name'];
                    }
                }),
                type: 'column',
                renderer: function (api, rowIdx, columns) {
                    const data = columns
                        .filter(col => col.columnIndex !== 11) // Action column hariç
                        .map(function (col) {
                            return col.title !== '' // Do not show row in modal popup if title is blank (for check box)
                                ? `<tr data-dt-row="${col.rowIndex}" data-dt-column="${col.columnIndex}">
      <td>${col.title}:</td>
      <td>${col.data}</td>
    </tr>`
                                : '';
                        })
                        .join('');

                    if (data) {
                        const div = document.createElement('div');
                        div.classList.add('table-responsive');
                        const table = document.createElement('table');
                        div.appendChild(table);
                        table.classList.add('table');
                        const tbody = document.createElement('tbody');
                        tbody.innerHTML = data;
                        table.appendChild(tbody);
                        return div;
                    }
                    return false;
                }
            }
        },
        initComplete: function () {
            const host = document.querySelector('.dt-search')?.closest('.dt-layout-end');
            if (!host) return;

            if (!host.querySelector('.custom-create-btn')) {
                host.insertAdjacentHTML('beforeend', `
                    <div class="custom-create-btn ms-md-3">
                        <div class="btn-group">
                            <button type="button" class="btn btn-primary dropdown-toggle" data-bs-toggle="dropdown">
                                <i class="bx bx-plus me-1"></i>
                                <span class="d-none d-sm-inline-block">Create</span>
                            </button>
                            <ul class="dropdown-menu dropdown-menu-end">
                                <li><a class="dropdown-item create-task"><i class="bx bx-task me-2"></i> Task</a></li>
                                <li><a class="dropdown-item create-meeting"><i class="bx bx-calendar-event me-2"></i> Meeting</a></li>
                            </ul>
                        </div>
                    </div>
                `);
            }

            fixDataTableLayout();
        },
        drawCallback: function () {
            fixDataTableLayout(); // her yeniden çizimde stil uygula (sayfalama, filtre vs.)
        }
    });
}
function fixDataTableLayout() {
    const elementsToModify = [
        {
            selector: '.dt-buttons > .btn:not(.dropdown-toggle)',
            classToRemove: 'btn-secondary'
        },
        { selector: '.dt-search .form-control', classToRemove: 'form-control-sm' },
        { selector: '.dt-length .form-select', classToRemove: 'form-select-sm', classToAdd: 'ms-0' },
        { selector: '.dt-length', classToAdd: 'mb-md-6 mb-0' },
        { selector: '.dt-search', classToAdd: 'mb-md-6 mb-2' },
        {
            selector: '.dt-layout-end',
            classToRemove: 'justify-content-between',
            classToAdd: 'd-flex gap-md-4 justify-content-md-between justify-content-center gap-2 flex-wrap mt-0'
        },
        { selector: '.dt-layout-start', classToAdd: 'mt-0' },
        {
            selector: '.dt-buttons',
            classToRemove: 'gap-4',
            classToAdd: 'd-flex align-items-center gap-2 mb-md-0 mb-6'
        },
        { selector: '.dt-layout-table', classToRemove: 'row mt-2' },
        { selector: '.dt-layout-full', classToRemove: 'col-md col-12', classToAdd: 'table-responsive' }
    ];

    // Delete record
    elementsToModify.forEach(({ selector, classToRemove, classToAdd }) => {
        document.querySelectorAll(selector).forEach(element => {
            if (classToRemove) {
                classToRemove.split(' ').forEach(className => element.classList.remove(className));
            }
            if (classToAdd) {
                classToAdd.split(' ').forEach(className => element.classList.add(className));
            }
        });
    });
}
function capitalize(text) {
    if (!text) return text;
    return text.charAt(0).toUpperCase() + text.slice(1);
}
// --- BREADCRUMB LOGIC ---

function getTaskPathFromUrl() {
    const params = new URLSearchParams(window.location.search);
    const raw = params.get('taskPath');
    return raw ? raw.split('|') : [];
}

function updateQueryParam(key, value) {
    const params = new URLSearchParams(window.location.search);
    if (value === null || value === undefined) {
        params.delete(key);
    } else {
        params.set(key, value);
    }
    return params.toString();
}

function navigateToTask(taskId, taskName) {
    const params = new URLSearchParams(window.location.search);

    const projectName = params.get('projectName') || '';
    const workstreamName = params.get('workstreamName') || '';
    const workFlowId = params.get('workFlowId') || '';

    const taskPath = getTaskPathFromUrl();
    taskPath.push(taskName);
    params.set('taskPath', taskPath.join('|'));

    if (projectName) params.set('projectName', projectName);
    if (workstreamName) params.set('workstreamName', workstreamName);
    if (workFlowId) params.set('workFlowId', workFlowId);

    const targetUrl = `/ppm/${taskId}/workstream-tasks?${params.toString()}`;
    window.location.href = targetUrl;
}

function pushTaskToPath(taskName, reload = true) {
    const params = new URLSearchParams(window.location.search);
    const path = getTaskPathFromUrl();
    path.push(taskName);
    params.set('taskPath', path.join('|'));

    if (reload) {
        window.location.search = params.toString();
    } else {
        const newUrl = `${window.location.pathname}?${params.toString()}`;
        window.history.pushState({ path: newUrl }, '', newUrl);
        renderBreadcrumb();
    }
}

function renderBreadcrumb() {
    const params = new URLSearchParams(window.location.search);
    // Project & Workstream from URL
    const projectName = params.get('projectName') || 'Project';
    const workstreamName = params.get('workstreamName') || 'Workstream';
    const taskPath = getTaskPathFromUrl();
    const workFlowId = params.get('workFlowId') || '';

    const $breadcrumb = $('ol.breadcrumb');
    if (!$breadcrumb.length) return;

    $breadcrumb.empty();

    // 1. Project (Fixed)
    // Reconstruct project link if possible, otherwise generic
    $breadcrumb.append(`
            <li class="breadcrumb-item allow-in-view">
                <a href="/ppm/workflow-overview">${capitalize(projectName)}</a>
            </li>
        `);

    // 2. Workstream (Fixed)
    let wsHref = '#';
    if (workFlowId) {
        wsHref = `/ppm/${encodeURIComponent(workFlowId)}/workstream?workstreamName=${encodeURIComponent(projectName)}`;
    }

    $breadcrumb.append(`
            <li class="breadcrumb-item allow-in-view">
                <a href="${wsHref}">${capitalize(workstreamName)}</a>
            </li>
        `);

    // 3. Dynamic Task Stack
    taskPath.forEach((task, index) => {
        const partialPath = taskPath.slice(0, index + 1).join('|');
        // We must preserve other params too
        const href = `?${updateQueryParam('taskPath', partialPath)}`;
        $breadcrumb.append(`
                <li class="breadcrumb-item">
                    <a href="${href}">${task}</a>
                </li>
            `);
    });

    // 4. Current Page (Fixed "Tasks")
    $breadcrumb.append(`
            <li class="breadcrumb-item active" id="breadcrumbActive">
                <span class="text-primary">Tasks</span>
            </li>
        `);
}

// Capture task drill-down
$(document).on('click', '.workstream-task-name', function () {
    const taskId = $(this).data('task-id');
    const taskName = $(this).text().trim();
    if (taskId && taskName) {
        navigateToTask(taskId, taskName);
    }
});

$(document).on('click', '.open-record', function (e) {
    e.preventDefault();
    const tr = $(this).closest('tr');
    const taskId = tr.find('.workstream-task-name').data('task-id');
    const taskName = tr.find('.workstream-task-name').text().trim();
    if (taskId && taskName) {
        navigateToTask(taskId, taskName);
    }
});




function initCreateTaskComponents() {
    // 1. Quill Initialization
    const fullToolbar = [
        [{ font: [] }, { size: [] }],
        ['bold', 'italic', 'underline', 'strike'],
        [{ color: [] }, { background: [] }],
        [{ script: 'super' }, { script: 'sub' }],
        [{ header: '1' }, { header: '2' }, 'blockquote', 'code-block'],
        [{ list: 'ordered' }, { indent: '-1' }, { indent: '+1' }],
        [{ direction: 'rtl' }, { align: [] }],
        ['link', 'image', 'video', 'formula'],
        ['clean']
    ];

    // Make accessible globally for reset
    window.taskDescriptionQuill = null;

    if (document.querySelector('#task-description')) {
        window.taskDescriptionQuill = new Quill('#task-description', {
            bounds: '#task-description',
            placeholder: 'Type Something...',
            modules: {
                syntax: true,
                toolbar: fullToolbar
            },
            theme: 'snow'
        });
    }

    // 2. Select2 Initialization
    const select2Ids = ['#task-category', '#follow-up-user'];
    select2Ids.forEach(id => {
        const el = $(id);
        if (el.length) {
            el.select2({
                placeholder: el.attr('placeholder'),
                allowClear: true,
                dropdownParent: el.closest('.card-body') // Ensure dropdown works in hidden container/modal
            });
        }
    });

    // Assignee Select2 & Toggle Logic
    const $assigneeSelect = $('#task-assignee');
    const $assignMultipleCheckbox = $('#assign-multiple');
    const $multiAssignHelp = $('#multi-assign-alert');

    function initAssigneeSelect(isMultiple) {
        if ($assigneeSelect.data('select2')) {
            $assigneeSelect.select2('destroy');
        }

        $assigneeSelect.attr('multiple', isMultiple);

        $assigneeSelect.select2({
            placeholder: 'Select Assignee',
            allowClear: true,
            dropdownParent: $assigneeSelect.closest('.card-body')
        });
    }

    // Initial init (Single)
    if ($assigneeSelect.length) {
        initAssigneeSelect(false);
    }

    if ($assignMultipleCheckbox.length) {
        $assignMultipleCheckbox.on('change', function () {
            const isChecked = $(this).is(':checked');
            // Toggle Multiple Mode
            initAssigneeSelect(isChecked);

            // Toggle Help Text
            if (isChecked) {
                $multiAssignHelp.removeClass('d-none');
            } else {
                $multiAssignHelp.addClass('d-none');
            }
        });
    }


    // 3. Flatpickr Initialization
    const startDateEl = document.querySelector('#task-start-date');
    const dueDateEl = document.querySelector('#task-due-date');

    // Make accessible globally for reset
    window.startDatePicker = null;
    window.dueDatePicker = null;

    if (startDateEl && dueDateEl) {
        const today = new Date();
        window.startDatePicker = flatpickr(startDateEl, {
            dateFormat: 'Y-m-d',
            altInput: true,
            altFormat: 'd M Y',
            allowInput: true,
            minDate: 'today',
            defaultDate: today, // ✅ BUGÜN SEÇİLİ
            static: true,
            onChange(selectedDates) {
                if (selectedDates.length) {
                    window.dueDatePicker.set('minDate', selectedDates[0]);
                }
            }
        });

        window.dueDatePicker = flatpickr(dueDateEl, {
            dateFormat: 'Y-m-d',
            altInput: true,
            altFormat: 'd M Y',
            allowInput: true,
            minDate: 'today',
            static: true
        });
    }

    // 5. Dependencies Logic
    const $depTaskSelect = $('#dependency-task-select');
    const $depTypeSelect = $('#dependency-type-select');
    const $depDirectionSelect = $('#dependency-direction-select');
    const $depReasonSelect = $('#dependency-reason-select');
    const $depList = $('#dependencies-list');


    // Init Select2 for Dependencies
    [$depTaskSelect, $depTypeSelect, $depDirectionSelect, $depReasonSelect].forEach(el => {
        if (el.length) {
            el.select2({
                placeholder: el.attr('data-placeholder') || 'Select value',
                allowClear: true,
                dropdownParent: el.closest('.card-body')
            });
        }
    });



    // Drag and Drop Handlers
    function initDragAndDrop() {
        // ... (Same as before)
        const container = document.getElementById('dependencies-list');
        if (!container) return;
        let draggedItem = null;
        container.addEventListener('dragstart', (e) => {
            if (e.target.classList.contains('alert')) { // Only drag dependency alerts
                draggedItem = e.target;
                e.target.style.opacity = '0.5';
                e.dataTransfer.effectAllowed = 'move';
            }
        });
        container.addEventListener('dragend', (e) => {
            if (e.target.classList.contains('alert')) {
                e.target.style.opacity = '1';
                draggedItem = null;
            }
        });
        container.addEventListener('dragover', (e) => {
            e.preventDefault();
            const afterElement = getDragAfterElement(container, e.clientY);
            if (draggedItem) {
                if (afterElement == null) { container.appendChild(draggedItem); }
                else { container.insertBefore(draggedItem, afterElement); }
            }
        });
    }
    function getDragAfterElement(container, y) {
        const draggableElements = [...container.querySelectorAll('.alert:not(.dragging)')]; // Select only alerts
        return draggableElements.reduce((closest, child) => {
            const box = child.getBoundingClientRect();
            const offset = y - box.top - box.height / 2;
            if (offset < 0 && offset > closest.offset) { return { offset: offset, element: child }; }
            else { return closest; }
        }, { offset: Number.NEGATIVE_INFINITY }).element;
    }
    initDragAndDrop();

    function addDependency() {
        const taskId = $depTaskSelect.val();
        const taskName = $depTaskSelect.find('option:selected').text();
        const typeId = $depTypeSelect.val();
        const typeName = $depTypeSelect.find('option:selected').text();
        const direction = $depDirectionSelect.val(); // 'blocked_by' or 'blocks'
        const reasonCols = $depReasonSelect.select2('data'); // Array of objects

        // VALIDATION: Task, Type AND Direction are required
        if (!taskId || !typeId || !direction) return;

        // 1. Duplicate Check (Consider Direction too? Usually same connection reversed is different semantic but same link. Simplest is check Task constraint)
        const isDuplicate = $depList.find(`.alert[data-task-id="${taskId}"]`).length > 0;

        if (isDuplicate) {
            const $alert = $('#dependency-duplicate-alert');
            $alert.removeClass('d-none');
            setTimeout(() => { $alert.addClass('d-none'); }, 3000);
            // Clear
            $depTaskSelect.val(null).trigger('change.select2');
            $depTypeSelect.val(null).trigger('change.select2');
            $depDirectionSelect.val(null).trigger('change.select2');
            $depReasonSelect.val(null).trigger('change.select2');
            return;
        }

        // 2. Prepare Data
        const typeInfo = dependencyTypeInfo[typeId] || { label: typeName, description: '' };

        // Direction Badge
        let directionBadge = '';
        if (direction === 'blocks') {
            directionBadge = `<span class="badge bg-label-primary ms-1" data-bs-toggle="tooltip" title="This task blocks the selected task from progressing."> Blocks</span>`;
        } else {
            directionBadge = `<span class="badge bg-label-danger ms-1" data-bs-toggle="tooltip" title ="This task cannot proceed until the selected task is completed."> Blocked By</span>`;
        }

        // Reason Badges
        let reasonHtml = '';
        if (reasonCols && reasonCols.length > 0) {
            reasonHtml = reasonCols.map(r => `<span class="badge bg-lighter text-body ms-1 small" style="font-size:0.75rem;"> ${r.text}</span>`).join('');
        }


        // SVG Preview - Direction Aware
        const svgPreview = getDependencySvg(typeId, direction);

        // Graph Icon
        const graphIcon = `
            <div class="position-relative d-inline-block dep-graph-hover">
                <i class="bx bx-network-chart text-muted cursor-pointer fs-5 align-middle"></i>
                <div class="dep-svg-preview rounded bg-white p-2 border position-absolute top-100 start-50 translate-middle-x mt-2 d-none" style="z-index: 10; width: 170px;">
                    ${svgPreview}
                    <div class="text-center small text-muted mt-1">${typeInfo.label}</div>
                </div>
            </div>
        `;

        const currentTaskName = $('#task-name').val() || '';

        // 3. Create Alert Item
        const itemHtml = `
            <div class="alert alert-outline-secondary d-flex align-items-center justify-content-between"
                 draggable="true"
                 data-task-id="${taskId}"
                 data-dep-type="${typeId}"
                 data-direction="${direction}"
                 data-reasons='${JSON.stringify(reasonCols.map(r => r.id))}'>
                <div class="d-flex align-items-center gap-3">
                    <i class="bx bx-grid-vertical text-muted cursor-grab" style="cursor: grab;"></i>
                    <div>
                        <div class="d-flex align-items-center gap-2 mb-1 flex-wrap">
                            <h5 class="card-title mb-0">${taskName}</h5>
                            ${graphIcon}
                            <span class="badge bg-label-info">Todo</span>
                            <span class="badge bg-label-warning">High</span>
                            ${directionBadge}
                            ${reasonHtml}
                        </div>
                        <small class="text-muted">
                           <span class="fw-medium text-body dependency-task-name">${currentTaskName}</span>
                        </small>
                    </div>
                </div>
                <div class="col-auto d-flex align-items-center">
                     <a href="javascript:;" class="btn btn-icon delete-record remove-dependency-btn" title="Delete dependency">
                        <i class="icon-base bx bx-trash icon-md"></i>
                     </a>
                </div>
            </div>
        `;

        const $newItem = $(itemHtml);
        $depList.append($newItem);

        // Re-init tooltips
        $newItem.find('[data-bs-toggle="tooltip"]').each(function () { new bootstrap.Tooltip(this); });

        // Hover events
        $newItem.find('.dep-graph-hover').hover(
            function () { $(this).find('.dep-svg-preview').removeClass('d-none'); },
            function () { $(this).find('.dep-svg-preview').addClass('d-none'); }
        );

        // Reset All
        $depTaskSelect.val(null).trigger('change.select2');
        $depTypeSelect.val(null).trigger('change.select2');
        $depDirectionSelect.val(null).trigger('change.select2');
        $depReasonSelect.val(null).trigger('change.select2');
    }

    // Auto-Add Logic Handlers: Only try if ALL 3 required are present
    const tryAutoAdd = function () {
        if ($depTaskSelect.val() && $depTypeSelect.val() && $depDirectionSelect.val()) {
            addDependency();
        }
    };

    $depTaskSelect.on('select2:select', tryAutoAdd);
    $depTypeSelect.on('select2:select', tryAutoAdd);
    $depDirectionSelect.on('select2:select', tryAutoAdd); // Trigger on direction too

    // Sync Task Name
    $('#task-name').on('input', function () {
        const val = $(this).val();
        $('.dependency-task-name').text(val);
    });

    // Remove
    if ($depList.length) {
        $depList.on('click', '.remove-dependency-btn', function () {
            $(this).closest('.alert').remove();
        });
    }


    // Full Graph Modal - Safe Init
    const modalEl = document.getElementById('dependencyGraphModal');
    if (modalEl) {
        modalEl.addEventListener('show.bs.modal', function () {
            const $container = $('#dependency-graph-container');
            $container.css({ 'position': 'relative', 'height': '500px', 'overflow': 'hidden', 'background': '#f8f9fa' });
            $container.empty();

            const items = $depList.find('.alert');

            if (items.length === 0) {
                $container.html('<div class="d-flex h-100 align-items-center justify-content-center text-muted">No dependencies to visualize.</div>');
                return;
            }

            // Calculation Safe Width/Height
            const containerEl = $container[0];
            let w = containerEl.clientWidth || 800;
            let h = 500;
            if (w < 100) w = 800;

            // --- Configuration ---
            const nodeWidth = 180;
            const nodeHeight = 50;
            const gap = 20;

            // 3-Column Layout Calculations
            const leftX = 50;
            const centerX = (w / 2) - (nodeWidth / 2);
            const rightX = w - nodeWidth - 50;

            // Separate Items by Direction
            const blockedByItems = [];
            const blocksItems = [];

            items.each(function () {
                const direction = $(this).attr('data-direction');
                // If 'blocks', goes to Right. Default/blocked_by goes to Left.
                if (direction === 'blocks') {
                    blocksItems.push($(this));
                } else {
                    blockedByItems.push($(this));
                }
            });

            // Calculate Vertical Centering for each column
            function getStartTop(count) {
                const totalH = count * nodeHeight + (count - 1) * gap;
                let top = (h - totalH) / 2;
                return top < 20 ? 20 : top;
            }

            const blockedByStartTop = getStartTop(blockedByItems.length);
            const blocksStartTop = getStartTop(blocksItems.length);
            const thisY = (h - nodeHeight) / 2; // Center "This Node"

            // --- SVG Construction ---
            const svgNS = "http://www.w3.org/2000/svg";
            const svg = document.createElementNS(svgNS, "svg");
            svg.setAttribute("width", "100%");
            svg.setAttribute("height", "100%");
            svg.setAttribute("viewBox", `0 0 ${w} ${h} `);
            svg.style.overflow = "visible";

            // Defs (Marker)
            const defs = document.createElementNS(svgNS, "defs");
            const marker = document.createElementNS(svgNS, "marker");
            marker.setAttribute("id", "arrowhead");
            marker.setAttribute("markerWidth", "10");
            marker.setAttribute("markerHeight", "7");
            marker.setAttribute("refX", "9");
            marker.setAttribute("refY", "3.5");
            marker.setAttribute("orient", "auto");
            const polygon = document.createElementNS(svgNS, "polygon");
            polygon.setAttribute("points", "0 0, 10 3.5, 0 7");
            polygon.setAttribute("fill", "#696cff");
            marker.appendChild(polygon);
            defs.appendChild(marker);
            svg.appendChild(defs);

            // Helpers: Create Node
            function createNode(x, y, text, isPrimary, typeBadge = '') {
                const g = document.createElementNS(svgNS, "g");

                const rect = document.createElementNS(svgNS, "rect");
                rect.setAttribute("x", x);
                rect.setAttribute("y", y);
                rect.setAttribute("width", nodeWidth);
                rect.setAttribute("height", nodeHeight);
                rect.setAttribute("rx", "6");
                rect.setAttribute("fill", "white");
                rect.setAttribute("stroke", isPrimary ? "#696cff" : "#d9dee3");
                rect.setAttribute("stroke-width", isPrimary ? "2" : "1");

                const txt = document.createElementNS(svgNS, "text");
                txt.setAttribute("x", x + nodeWidth / 2);
                txt.setAttribute("y", y + nodeHeight / 2);
                txt.setAttribute("dy", "0.35em");
                txt.setAttribute("text-anchor", "middle");
                txt.setAttribute("fill", "#566a7f");
                txt.setAttribute("font-size", "13");
                txt.setAttribute("font-weight", isPrimary ? "bold" : "normal");

                let displayUrl = text;
                if (text.length > 20) displayUrl = text.substring(0, 18) + '...';
                txt.textContent = displayUrl;

                const title = document.createElementNS(svgNS, "title");
                title.textContent = text;
                g.appendChild(title);

                g.appendChild(rect);
                g.appendChild(txt);

                if (typeBadge) {
                    const badge = document.createElementNS(svgNS, "text");
                    badge.setAttribute("x", x + nodeWidth - 10);
                    badge.setAttribute("y", y + 15);
                    badge.setAttribute("text-anchor", "end");
                    badge.setAttribute("fill", "#a1acb8");
                    badge.setAttribute("font-size", "10");
                    badge.textContent = typeBadge;
                    g.appendChild(badge);
                }
                return g;
            }

            // Helper: Draw Path
            function drawLink(startX, startY, endX, endY) {
                const path = document.createElementNS(svgNS, "path");

                // Single consolidated Bezier formula
                const cp = Math.abs(endX - startX) / 2;
                const c1x = startX + (startX < endX ? cp : -cp);
                const c2x = endX + (startX < endX ? -cp : cp);

                const d = `M ${startX} ${startY} C ${c1x} ${startY}, ${c2x} ${endY}, ${endX} ${endY} `;

                path.setAttribute("d", d);
                path.setAttribute("stroke", "#696cff");
                path.setAttribute("stroke-width", "2");
                path.setAttribute("fill", "none");
                path.setAttribute("opacity", "0.8");
                path.setAttribute("marker-end", "url(#arrowhead)");

                svg.insertBefore(path, defs.nextSibling);
            }

            // Helper: Get Connection Points (Type Aware)
            function getLinkPoints(fromRect, toRect, type) {
                // Determine Left/Right based on flow
                // Flow is always From -> To

                // standard: from is left, to is right
                const startX = fromRect.x + fromRect.w; // Start from right edge
                const endX = toRect.x;                  // End at left edge

                // Y Calc based on Type (SS, FS, SF, FF)
                // SS: Start -> Start (Top 30% -> Top 30%)
                // FS: Finish -> Start (Bottom 70% -> Top 30%)
                // SF: Start -> Finish (Top 30% -> Bottom 70%)
                // FF: Finish -> Finish (Bottom 70% -> Bottom 70%)

                const top = 0.30;
                const bottom = 0.70;

                const y1Top = fromRect.y + (fromRect.h * top);
                const y1Bot = fromRect.y + (fromRect.h * bottom);
                const y2Top = toRect.y + (toRect.h * top);
                const y2Bot = toRect.y + (toRect.h * bottom);

                let startY, endY;

                if (type === 'SS') { startY = y1Top; endY = y2Top; }
                else if (type === 'FS') { startY = y1Bot; endY = y2Top; }
                else if (type === 'SF') { startY = y1Top; endY = y2Bot; }
                else { startY = y1Bot; endY = y2Bot; } // FF

                return { startX, startY, endX, endY };
            }

            // 1. Render Blocked By Items (Left Column)
            blockedByItems.forEach((item, index) => {
                const name = item.find('h5.card-title').text();
                const type = item.attr('data-dep-type');
                const y = blockedByStartTop + index * (nodeHeight + gap);

                const typeCode = dependencyTypeInfo[type]?.code || type;
                svg.appendChild(createNode(leftX, y, name, false, typeCode + ' | Blocked By'));

                // Link: BlockedByNode -> ThisNode
                const coords = getLinkPoints(
                    { x: leftX, y: y, w: nodeWidth, h: nodeHeight },
                    { x: centerX, y: thisY, w: nodeWidth, h: nodeHeight },
                    type
                );
                drawLink(coords.startX, coords.startY, coords.endX, coords.endY);
            });

            // 2. Render This Task (Center Column)
            const thisName = $('#task-name').val() || 'New Task';
            svg.appendChild(createNode(centerX, thisY, thisName, true));

            // Label
            const label = document.createElementNS(svgNS, "text");
            label.setAttribute("x", centerX + nodeWidth / 2);
            label.setAttribute("y", thisY - 10);
            label.setAttribute("text-anchor", "middle");
            label.setAttribute("fill", "#696cff");
            label.setAttribute("font-size", "12");
            label.setAttribute("font-weight", "bold");
            label.textContent = "This Task";
            svg.appendChild(label);


            // 3. Render Blocks Items (Right Column)
            blocksItems.forEach((item, index) => {
                const name = item.find('h5.card-title').text();
                const type = item.attr('data-dep-type');
                const y = blocksStartTop + index * (nodeHeight + gap);

                const typeCode = dependencyTypeInfo[type]?.code || type;
                svg.appendChild(createNode(rightX, y, name, false, typeCode + ' | Blocks'));

                // Link: ThisNode -> BlocksNode
                const coords = getLinkPoints(
                    { x: centerX, y: thisY, w: nodeWidth, h: nodeHeight },
                    { x: rightX, y: y, w: nodeWidth, h: nodeHeight },
                    type
                );
                drawLink(coords.startX, coords.startY, coords.endX, coords.endY);
            });

            $container.append(svg);
        });
    }

    // 4. Tooltip Initialization logic kept from before...

    // 6. Checklist Logic (Kept same) until end of file...

    // 6. Checklist Logic
    const $checklistName = $('#checklist-item-name');
    const $checklistDesc = $('#checklist-item-desc');
    const $checklistRequired = $('#checklist-item-required');
    const $addChecklistBtn = $('#add-checklist-item-btn');
    const $checklistContainer = $('#checklist-items-container');

    function addChecklistItem() {
        const name = $checklistName.val().trim();
        const desc = $checklistDesc.val().trim();
        const isRequired = $checklistRequired.is(':checked');

        if (!name) {
            $checklistName.addClass('is-invalid');
            return;
        }
        $checklistName.removeClass('is-invalid');

        const requiredBadge = isRequired
            ? `<span class="badge bg-label-warning ms-2">Required</span>`
            : '';

        const itemHtml = `
            <div class="border rounded p-2 d-flex justify-content-between align-items-start checklist-item mb-2"
                 draggable="true"
                 data-required="${isRequired}"
                 data-name="${name}"
                 data-desc="${desc}">
                <div class="d-flex gap-2 align-items-center">
                    <i class="bx bx-grid-vertical card-handle cursor-move" style="font-size:36px;"></i>
                    <div>
                        <h5 class="card-title mb-1">
                            ${name}
                            ${requiredBadge}
                        </h5>
                        ${desc ? `<p class="text-muted small mb-0">${desc}</p>` : ''}
                    </div>
                </div>
                <a href="javascript:;" class="btn btn-icon delete-checklist-item text-danger" title="Delete item">
                    <i class="icon-base bx bx-trash icon-md"></i>
                </a>
            </div>
        `;

        $checklistContainer.append(itemHtml);

        // Reset Form
        $checklistName.val('');
        $checklistDesc.val('');
        $checklistRequired.prop('checked', false);
    }

    $addChecklistBtn.on('click', addChecklistItem);

    // Delete Item
    $checklistContainer.on('click', '.delete-checklist-item', function () {
        $(this).closest('.checklist-item').remove();
    });

    // Checklist Drag & Drop
    function initChecklistDragAndDrop() {
        const container = document.getElementById('checklist-items-container');
        if (!container) return;

        let draggedItem = null;

        container.addEventListener('dragstart', (e) => {
            if (e.target.classList.contains('checklist-item')) {
                draggedItem = e.target;
                e.target.style.opacity = '0.5';
                e.dataTransfer.effectAllowed = 'move';
                e.target.classList.add('dragging');
            }
        });

        container.addEventListener('dragend', (e) => {
            if (e.target.classList.contains('checklist-item')) {
                e.target.style.opacity = '1';
                e.target.classList.remove('dragging');
                draggedItem = null;
            }
        });

        container.addEventListener('dragover', (e) => {
            e.preventDefault();
            const afterElement = getChecklistDragAfterElement(container, e.clientY);
            const draggable = document.querySelector('.checklist-item.dragging');
            if (draggable) {
                if (afterElement == null) {
                    container.appendChild(draggable);
                } else {
                    container.insertBefore(draggable, afterElement);
                }
            }
        });
    }

    function getChecklistDragAfterElement(container, y) {
        const draggableElements = [...container.querySelectorAll('.checklist-item:not(.dragging)')];
        return draggableElements.reduce((closest, child) => {
            const box = child.getBoundingClientRect();
            const offset = y - box.top - box.height / 2;
            if (offset < 0 && offset > closest.offset) {
                return { offset: offset, element: child };
            } else {
                return closest;
            }
        }, { offset: Number.NEGATIVE_INFINITY }).element;
    }

    initChecklistDragAndDrop();
}

// Cache for users to avoid repeated API calls during filtering
let cachedUsers = [];



function getPriorityBadge(priorityId, priorityName) {
    const map = {
        0: { icon: 'icon-base bx bx-minus', color: 'secondary' },
        1: { icon: 'icon-base bx bx-chevrons-down', color: 'primary' },
        2: { icon: 'icon-base bx bx-menu', color: 'secondary' },
        3: { icon: 'icon-base bx bx-chevron-up', color: 'warning' },
        4: { icon: 'icon-base bx bx-chevrons-up', color: 'danger' }
    };

    const key = Number(priorityId);
    const cfg = map[key] || map[2];

    return `<span class="d-flex align-items-center gap-2"><i class="${cfg.icon} text-${cfg.color} fs-5"></i><span>${priorityName}</span></span>`;
}

function formatPriorityOption(state) {
    if (!state.id) return state.text;
    return getPriorityBadge(state.id, state.text);
}


function getStatusBadge(statusId, statusName) {
    const map = {
        1: 'warning',   // New / To Do
        2: 'primary',   // In Progress
        3: 'success',   // Completed
        4: 'danger'     // Canceled
    };

    const key = Number(statusId);
    const badgeClass = map[key] || 'secondary';

    return `<span class="badge bg-${badgeClass}">${statusName}</span>`;
}

function formatStatusOption(state) {
    if (!state.id) return state.text;

    const statusId = state.element.dataset.status;
    return getStatusBadge(statusId, state.text);
}

async function loadDependencyTypes() {
    const $el = $('#dependency-type-select');
    if (!$el.length) return;

    const url = `${API.ppm}/Task/GetDependenciesType`;

    try {
        const response = await fetch(url);
        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        const types = result.data || result || [];

        $el.empty().append('<option></option>');
        types.forEach(x => {
            $el.append(`<option value="${x.id}">${x.name}</option>`);
        });
        $el.trigger('change');
    } catch (error) {
        console.error('Error loading dependency types:', error);
    }
}

async function loadTaskStatus(isEditMode = false) {
    const $el = $('#task-status');
    if (!$el.length) return;

    const url = `${API.ppm}/WorkflowCategory/GetWorkflowStatus`;

    try {
        const response = await fetch(url, {
            method: 'GET',
            headers: { 'Content-Type': 'application/json' }
        });

        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        const statusList = result.data || result || [];

        // 1. Destroy if already initialized
        if ($el.hasClass('select2-hidden-accessible')) {
            $el.select2('destroy');
        }

        // 2. Clear and Add default empty option
        $el.empty().append('<option></option>');

        // 3. Append Statuses (Filter out Completed if not edit mode)
        statusList.forEach(x => {
            if (!isEditMode && Number(x.id) === 3) return; // Skip Completed in Create mode

            $el.append(`
            <option value="${x.id}" data-status="${x.id}" >
                ${x.name}
              </option>
            `);
        });

        // 4. Re-init Select2
        $el.select2({
            placeholder: 'Select status',
            allowClear: true,
            width: '100%',
            templateResult: formatStatusOption,
            templateSelection: formatStatusOption,
            escapeMarkup: m => m
        });

        // 5. Default reset
        $el.val(null).trigger('change');

    } catch (error) {
        console.error('Error loading task statuses:', error);
    }
}

async function loadDependencyDirections() {
    const $el = $('#dependency-direction-select');
    if (!$el.length) return;

    const url = `${API.ppm}/Task/GetDependencyDirections`;

    try {
        const response = await fetch(url);
        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        const directions = result.data || result || [];

        // id to key mapping (1 -> blocked_by, 2 -> blocks)
        const map = {
            1: "blocked_by",
            2: "blocks"
        };

        $el.empty().append('<option></option>');
        directions.forEach(x => {
            const key = map[x.id] || x.name.toLowerCase().replace(' ', '_');
            $el.append(`<option value="${key}">${x.name}</option>`);
        });
        $el.trigger('change');
    } catch (error) {
        console.error('Error loading dependency directions:', error);
    }
}

async function loadDependencyReasons() {
    const $el = $('#dependency-reason-select');
    if (!$el.length) return;

    const url = `${API.ppm}/Task/GetDependencyReasons`;

    try {
        const response = await fetch(url);
        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        const reasons = result.data || result || [];

        $el.empty();
        reasons.forEach(x => {
            dependencyReasonMap[x.id] = x.name;
            $el.append(`<option value="${x.id}">${x.name}</option>`);
        });
        $el.trigger('change');
    } catch (error) {
        console.error('Error loading dependency reasons:', error);
    }
}

async function loadDependencyTasks(currentTaskId = "") {
    const $el = $('#dependency-task-select');
    if (!$el.length) return;

    const url = `${API.ppm}/Task/GetDependencyTasks?currentTaskId=${currentTaskId || null}`;

    try {
        const response = await fetch(url);
        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        const tasks = result.data || result || [];

        $el.empty().append('<option></option>');
        tasks.forEach(task => {
            const option = new Option(task.name, task.id, false, false);
            $(option).attr('data-cycle-risk', task.hasCycleRisk || false);
            $el.append(option);
        });
        $el.trigger('change');
    } catch (error) {
        console.error('Error loading dependency tasks:', error);
    }
}

async function loadTaskPriority() {
    const $el = $('#task-priority');
    if (!$el.length) return;

    const url = `${API.ppm}/WorkflowCategory/GetPriorities`;

    try {
        const response = await fetch(url, {
            method: 'GET',
            headers: { 'Content-Type': 'application/json' }
        });

        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        const priorities = result.data || result || [];

        // 1. Destroy if already initialized to apply new templates
        if ($el.hasClass('select2-hidden-accessible')) {
            $el.select2('destroy');
        }

        // 2. Clear and Add default empty option
        $el.empty().append('<option></option>');

        // 3. Append Priorities
        priorities.forEach(x => {
            $el.append(`<option value="${x.id}">${x.name}</option>`);
        });

        // 4. Re-init Select2 with templates
        $el.select2({
            placeholder: 'Select priority',
            allowClear: true,
            width: '100%',
            dropdownParent: $el.closest('.card-body'),
            templateResult: formatPriorityOption,
            templateSelection: formatPriorityOption,
            escapeMarkup: m => m
        });

        // 5. Default reset
        $el.val(null).trigger('change');
    } catch (error) {
        console.error('Error loading task priorities:', error);
    }
}

async function loadTaskCategories() {
    const $category = $('#task-category');
    if (!$category.length) return;

    const url = `${API.ppm}/Task/GetTaskCategory`;

    try {
        const response = await fetch(url, {
            method: 'GET',
            headers: { 'Content-Type': 'application/json' }
        });

        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        const categories = result.data || result || [];

        // 1. Clear and Add default empty option
        $category.empty().append('<option></option>');

        // 2. Append Categories
        categories.forEach(cat => {
            if (Number(cat.id) > 10) return; // SKIP if id > 10

            const categoryName = cat.name || 'Unknown';
            const option = new Option(categoryName, cat.id, false, false);
            $category.append(option);
        });

        // 3. Update Select2 (Do not re-init, just trigger change)
        $category.trigger('change');

    } catch (error) {
        console.error('Error loading task categories:', error);
    }
}

async function loadAssignees() {
    const $assignee = $('#task-assignee');
    if (!$assignee.length) return;

    const url = `${API.legacy.user}/api/PvUser/User/GetUsersByTenantId`;

    try {
        const response = await fetch(url, {
            method: 'GET',
            headers: { 'Content-Type': 'application/json' }
        });

        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        cachedUsers = result.data || result || [];

        // 1. Clear and Add default empty option
        $assignee.empty().append('<option></option>');

        // 2. Append Users
        cachedUsers.forEach(user => {
            const fullName = user.fullName || `${user.firstName || ''} ${user.lastName || ''} `.trim() || 'Unknown';
            const option = new Option(fullName, user.id, false, false);
            $assignee.append(option);
        });

        // 3. Update Select2
        $assignee.trigger('change');

        // Initial load for Follow Up
        loadFollowUpUsers();

    } catch (error) {
        console.error('Error loading dynamic assignees:', error);
    }
}

function loadFollowUpUsers() {
    const $followUp = $('#follow-up-user');
    const $assignee = $('#task-assignee');
    if (!$followUp.length) return;

    // Get selected assignees (handle string or array)
    const rawVal = $assignee.val();
    const selectedAssignees = Array.isArray(rawVal) ? rawVal : (rawVal ? [rawVal] : []);

    // Filter Cached Users: Exclude those already selected as assignees
    const filteredUsers = cachedUsers.filter(user => !selectedAssignees.includes(String(user.id)));

    // Track current follow-up selection to check if it needs clearing
    const currentFollowUpId = $followUp.val();

    // 1. Clear and Add default empty option
    $followUp.empty().append('<option></option>');

    // 2. Append Filtered Users
    filteredUsers.forEach(user => {
        const fullName = user.fullName || `${user.firstName || ''} ${user.lastName || ''} `.trim() || 'Unknown';
        const option = new Option(fullName, user.id, false, false);
        $followUp.append(option);
    });

    // 3. If previous selection is now filtered out (became an assignee), clear it
    if (selectedAssignees.includes(String(currentFollowUpId))) {
        $followUp.val(null);
    } else {
        $followUp.val(currentFollowUpId);
    }

    // 4. Update Select2
    $followUp.trigger('change');
}

function initCreateViewToggles() {
    const $mainContent = $('#workstream-main-content');
    const $createTaskContainer = $('#create-task-container');
    const $createMeetingContainer = $('#create-meeting-container');
    const $breadcrumbActive = $('#breadcrumbActive'); // Breadcrumb element
    let isTaskFormInitialized = false;
    let currentTaskMode = 'create'; // 'create' | 'edit' | 'view'

    // Helper to Reset Breadcrumb
    function resetBreadcrumb() {
        $breadcrumbActive.text('Tasks');
    }

    function applyViewModeUiRules() {
        // --- 1. TASK FORM ---
        const $taskForm = $('#create-task-form');
        if ($taskForm.length) {
            $taskForm.find('input, textarea').prop('readonly', true);
            $taskForm.find('select').each(function () {
                $(this).prop('disabled', true);
            });
        }

        // 2. Disable Assign Multiple Checkbox
        $('#assign-multiple').prop('disabled', true);

        // 3. Disable Task Quill
        if (window.taskDescriptionQuill) {
            window.taskDescriptionQuill.enable(false);
        }

        // 4. Hide/Disable Destructive & Add Actions
        $('.remove-dependency-btn, .delete-checklist-item').hide();
        $('#add-checklist-item-btn').hide();
        $('.card-handle').addClass('d-none');
        $('#checklist-item-name, #checklist-item-desc, #checklist-item-required').prop('disabled', true);

        // Disable Dependencies Selects explicitly
        $('#dependency-task-select, #dependency-type-select, #dependency-direction-select, #dependency-reason-select')
            .prop('disabled', true);

        // 5. Action Buttons
        $('[data-action="save-task"]').addClass('d-none');

        // General Close button behavior
        $('[data-action="cancel-create"]')
            .text('Close')
            .removeClass('btn-label-secondary')
            .addClass('btn-label-secondary');
    }

    function applyMeetingViewModeUiRules() {
        // --- 1. MEETING FORM ---
        const $meetingForm = $('#create-meeting-form');
        if ($meetingForm.length) {
            $meetingForm.find('input, textarea').prop('readonly', true);
            $meetingForm.find('select').each(function () {
                $(this).prop('disabled', true);
            });
        }

        // 2. Disable Meeting Quill
        if (window.meetingNotesQuill) {
            window.meetingNotesQuill.enable(false);
        }

        // 3. Agenda Actions
        $('#add-agenda-item-btn').addClass('d-none');
        $('.agenda-remove').addClass('d-none');

        // 4. Action Buttons
        $('[data-action="save-meeting"]').addClass('d-none');

        // General Close button behavior
        $('[data-action="cancel-create"]')
            .text('Close')
            .removeClass('btn-label-secondary')
            .addClass('btn-label-secondary');
    }

    function resetViewModeUiRules() {
        // 1. Enable Task Form Inputs
        $('#create-task-form').find('input, textarea').prop('readonly', false);
        $('#create-task-form').find('select').each(function () {
            $(this).prop('disabled', false);
        });

        if (window.taskDescriptionQuill) {
            window.taskDescriptionQuill.enable(true);
        }

        // 2. Enable Meeting Form Inputs
        const $mForm = $('#create-meeting-form');
        $mForm.find('input, textarea').prop('readonly', false);
        $mForm.find('select').each(function () {
            $(this).prop('disabled', false);
        });

        if (window.meetingNotesQuill) {
            window.meetingNotesQuill.enable(true);
        }

        // 3. Show Destructive & Add Actions (Task)
        $('.remove-dependency-btn, .delete-checklist-item').show();
        $('#add-checklist-item-btn').show();
        $('.card-handle').removeClass('d-none');
        $('#checklist-item-name, #checklist-item-desc, #checklist-item-required').prop('disabled', false);

        // 4. Show Agenda Actions (Meeting)
        $('#add-agenda-item-btn').removeClass('d-none');
        $('.agenda-remove').removeClass('d-none');

        // 5. Action Buttons
        $('[data-action="save-task"]').removeClass('d-none');
        $('[data-action="save-meeting"]').removeClass('d-none');
        $('[data-action="cancel-create"]').text('Cancel');
    }

    function applyEditModeAssigneeRules() {
        const $checkbox = $('#assign-multiple');
        const $help = $('#multi-assign-alert');

        if (!$checkbox.length) return;

        // Force single-assign
        $checkbox.prop('checked', false);
        $checkbox.prop('disabled', true);

        // Hide multi-assign help text
        $help.addClass('d-none');

        // Add tooltip explanation
        $checkbox
            .attr('data-bs-toggle', 'tooltip')
            .attr('data-bs-placement', 'top')
            .attr(
                'title',
                'Multiple assignee selection is only available when creating a new task.'
            );

        // Initialize tooltip safely
        new bootstrap.Tooltip($checkbox[0]);
    }

    function applyCreateModeActionButton() {
        const $btn = $('[data-action="save-task"]');
        if (!$btn.length) return;

        $btn
            .html('<i class="bx bx-save me-1"></i> Save')
            .removeClass('btn-warning')
            .addClass('btn-primary');
    }

    function applyEditModeActionButton() {
        const $btn = $('[data-action="save-task"]');
        if (!$btn.length) return;

        $btn
            .html('<i class="bx bx-edit me-1"></i> Update')
            .removeClass('btn-primary')
            .addClass('btn-warning');
    }

    function applyFollowUpTaskUiRules() {
        $('[data-bs-target="#navs-dependencies"]').addClass('disabled');
        $('#navs-dependencies').addClass('d-none');

        $('[data-bs-target="#navs-checklist"]').addClass('disabled');
        $('#navs-checklist').addClass('d-none');

        // Force switch to Details tab
        $('[data-bs-target="#navs-details"]').trigger('click');
    }

    function resetTaskUiRules() {
        $('[data-bs-target="#navs-dependencies"]').removeClass('disabled');
        $('#navs-dependencies').removeClass('d-none');

        $('[data-bs-target="#navs-checklist"]').removeClass('disabled');
        $('#navs-checklist').removeClass('d-none');

        $('[data-bs-target]').removeAttr('title');
    }

    function populateTaskBaseFields(data) {
        $('#task-name').val(data.name || '');

        if (window.taskDescriptionQuill) {
            window.taskDescriptionQuill.root.innerHTML = data.description || '';
        }

        $('#task-estimated-hour').val(data.ownEstimatedMinute || 0);

        if (window.startDatePicker) {
            window.startDatePicker.setDate(data.startDate || null, true);
        }

        if (window.dueDatePicker) {
            window.dueDatePicker.setDate(data.endDate || null, true);
        }

        // Follow-up fields
        if (data.followUpOfTask) {
            $('#fu-estimated-hour').val(data.followUpOfTask.estimatedMinute || 0);
        } else {
            $('#fu-estimated-hour').val('');
        }
    }

    async function populateTaskSelectFields(data) {
        await loadTaskStatus(true);
        if (data.statusId != null) {
            $('#task-status').val(String(data.statusId)).trigger('change');
        }

        await loadTaskCategories();
        if (data.categoryId != null) {
            $('#task-category').val(String(data.categoryId)).trigger('change');
        }

        await loadTaskPriority();
        if (data.priorityId != null) {
            $('#task-priority').val(String(data.priorityId)).trigger('change');
        }

        await loadAssignees();
        if (Array.isArray(data.assignees)) {
            const assigneeIds = data.assignees.map(a => a.id);
            $('#task-assignee').val(assigneeIds).trigger('change');
        }

        // Follow-up assignee (single)
        if (data.followUpOfTask?.assignees?.length) {
            const fuUserId = data.followUpOfTask.assignees[0].id;
            $('#follow-up-user').val(fuUserId).trigger('change');
        }
    }

    function hydrateDependenciesForEdit(dependencies) {
        const $depList = $('#dependencies-list');
        $depList.empty();

        if (!Array.isArray(dependencies) || dependencies.length === 0) return;

        dependencies.forEach(dep => {
            const typeInfo = dependencyTypeInfo[dep.dependencyTypeId] || {
                label: dep.dependencyTypeName,
                description: ''
            };

            const direction = dep.direction;
            const taskName = dep.dependencyTaskName;
            const taskId = dep.dependencyTaskId;
            const reasonIds = dep.reasonIds || [];

            // Direction badge
            const directionBadge =
                direction === 'blocks'
                    ? `<span class="badge bg-label-primary ms-1" > Blocks</span > `
                    : `<span class="badge bg-label-danger ms-1" > Blocked By</span> `;

            // Reason badges
            const reasonHtml = reasonIds.map(id => {
                const reason = dependencyReasonMap?.[id];
                return reason
                    ? `<span class="badge bg-lighter text-body ms-1 small" > ${reason}</span > `
                    : '';
            }).join('');

            // SVG preview
            const svgPreview = getDependencySvg(dep.dependencyTypeId, direction);

            const graphIcon = `
            <div class="position-relative d-inline-block dep-graph-hover">
                    <i class="bx bx-network-chart text-muted cursor-pointer fs-5 align-middle"></i>
                    <div class="dep-svg-preview rounded bg-white p-2 border position-absolute
                                top-100 start-50 translate-middle-x mt-2 d-none"
                         style="z-index:10;width:170px;">
                        ${svgPreview}
                        <div class="text-center small text-muted mt-1">
                            ${typeInfo.label}
                        </div>
                    </div>
                </div>
            `;

            const currentTaskName = $('#task-name').val() || '';

            const itemHtml = `
            <div class="alert alert-outline-secondary d-flex align-items-center justify-content-between"
        draggable = "true"
        data-task-id="${taskId}"
        data-dep-type="${dep.dependencyTypeId}"
        data-direction="${direction}"
        data-reasons='${JSON.stringify(reasonIds)}'>

    <div class="d-flex align-items-center gap-3">
        <i class="bx bx-grid-vertical text-muted cursor-grab"></i>

        <div>
            <div class="d-flex align-items-center gap-2 mb-1 flex-wrap">
                <h5 class="card-title mb-0">${taskName}</h5>
                ${graphIcon}
                ${directionBadge}
                ${reasonHtml}
            </div>
            <small class="text-muted">
                <span class="fw-medium text-body dependency-task-name">${currentTaskName}</span>
            </small>
        </div>
    </div>

    <a href="javascript:;" class="btn btn-icon delete-record remove-dependency-btn">
        <i class="bx bx-trash"></i>
    </a>
</div> `;

            const $item = $(itemHtml);
            $depList.append($item);

            // Tooltips
            $item.find('[data-bs-toggle="tooltip"]').each(function () {
                new bootstrap.Tooltip(this);
            });

            // Hover preview
            $item.find('.dep-graph-hover').hover(
                function () { $(this).find('.dep-svg-preview').removeClass('d-none'); },
                function () { $(this).find('.dep-svg-preview').addClass('d-none'); }
            );
        });
    }

    function populateChecklistItems(checklistItems) {
        const $container = $('#checklist-items-container');
        $container.empty();

        if (!Array.isArray(checklistItems) || checklistItems.length === 0) return;

        checklistItems.forEach(item => {
            const requiredBadge = item.isRequired
                ? `<span class="badge bg-label-warning ms-2">Required</span>`
                : '';

            const descHtml = item.description
                ? `<p class="text-muted small mb-0">${item.description}</p>`
                : '';

            const itemHtml = `
            <div class="border rounded p-2 d-flex justify-content-between align-items-start checklist-item mb-2"
                 draggable="true"
                 data-required="${item.isRequired}"
                 data-name="${item.name}"
                 data-desc="${item.description || ''}">
                <div class="d-flex gap-2 align-items-center">
                    <i class="bx bx-grid-vertical card-handle cursor-move" style="font-size:36px;"></i>
                    <div>
                        <h5 class="card-title mb-1">
                            ${item.name}
                            ${requiredBadge}
                        </h5>
                        ${descHtml}
                    </div>
                </div>
                <a href="javascript:;" class="btn btn-icon delete-checklist-item text-danger" title="Delete item">
                    <i class="icon-base bx bx-trash icon-md"></i>
                </a>
            </div>
            `;

            $container.append(itemHtml);
        });
    }

    // React to Assignee changes to filter Follow-Up list
    $(document).on('change', '#task-assignee', function () {
        loadFollowUpUsers();
    });

    // Validation Helpers
    function setSelect2Invalid($select, isInvalid) {
        const $container = $select
            .closest('.select2-hidden-accessible')
            .next('.select2');

        const $selection = $container.find('.select2-selection');

        if (!$selection.length) return;

        if (isInvalid) {
            $selection.addClass('is-invalid');
        } else {
            $selection.removeClass('is-invalid');
        }
    }

    function setFlatpickrInvalid($input, isInvalid) {
        const instance = $input[0]?._flatpickr;
        if (!instance) return;

        const $visibleInput = $(instance.altInput || instance.input);

        if (isInvalid) {
            $visibleInput.addClass('is-invalid');
        } else {
            $visibleInput.removeClass('is-invalid');
        }
    }

    function getParentTaskIdFromUrl() {
        const segments = window.location.pathname.split('/').filter(Boolean);
        const ppmIndex = segments.indexOf('ppm');
        if (ppmIndex > -1 && segments.length > ppmIndex + 1) {
            return segments[ppmIndex + 1];
        }
        return null;
    }

    function getWorkflowIdFromUrl() {
        return new URLSearchParams(window.location.search).get('workFlowId') || '';
    }

    function getProjectNameFromUrl() {
        return new URLSearchParams(window.location.search).get('projectName') || '';
    }

    function getWorkstreamNameFromUrl() {
        return new URLSearchParams(window.location.search).get('workstreamName') || '';
    }

    // Save Task Logic
    async function saveWorkstreamTask() {
        const name = $('#task-name').val()?.trim();
        const statusId = $('#task-status').val();
        let categoryId = $('#task-category').val();
        const priorityId = $('#task-priority').val();
        const startDateRaw = $('#task-start-date').val();
        const dueDateRaw = $('#task-due-date').val();
        const estimatedHour = $('#task-estimated-hour').val();
        const followUpUserId = $('#follow-up-user').val();
        const fuEstimatedHour = $('#fu-estimated-hour').val();
        const description = window.taskDescriptionQuill ? window.taskDescriptionQuill.root.innerHTML : '';

        // 1. Hard Validations
        const $taskName = $('#task-name');
        const $taskStatus = $('#task-status');
        const $startDate = $('#task-start-date');
        const $dueDate = $('#task-due-date');

        let isValid = true;

        // Reset
        $taskName.removeClass('is-invalid');
        setSelect2Invalid($taskStatus, false);
        setFlatpickrInvalid($startDate, false);
        setFlatpickrInvalid($dueDate, false);

        if (!name) {
            $taskName.addClass('is-invalid');
            isValid = false;
        }

        if (!statusId) {
            setSelect2Invalid($taskStatus, true);
            isValid = false;
        } else {
            setSelect2Invalid($taskStatus, false);
        }

        // 3. Date Validations
        if ((startDateRaw && !dueDateRaw) || (!startDateRaw && dueDateRaw)) {
            setFlatpickrInvalid($startDate, !startDateRaw);
            setFlatpickrInvalid($dueDate, !dueDateRaw);
            isValid = false;
        } else {
            setFlatpickrInvalid($startDate, false);
            setFlatpickrInvalid($dueDate, false);
        }

        if (startDateRaw && dueDateRaw) {
            const start = moment(startDateRaw);
            const due = moment(dueDateRaw);
            if (start.isAfter(due)) {
                setFlatpickrInvalid($startDate, true);
                setFlatpickrInvalid($dueDate, true);
                isValid = false;
            }
        }

        if (!isValid) return;

        // 2. Soft / Default Logic
        if (!categoryId) {
            categoryId = "10"; // Default to "Other" (based on map at top of file)
        }

        // 4. Collect Dynamic Data
        const assignees = $('#task-assignee').val();
        const assigneeList = Array.isArray(assignees) ? assignees : (assignees ? [assignees] : []);

        const dependencies = [];
        $('#dependencies-list .alert').each(function () {
            const $dep = $(this);
            dependencies.push({
                taskId: currentEditingTaskId || null,
                dependencyTaskId: $dep.attr('data-task-id'),
                dependencyTypeId: $dep.attr('data-dep-type'),
                direction: $dep.attr('data-direction'),
                reasonIds: JSON.parse($dep.attr('data-reasons') || '[]')
            });
        });

        const checklistItems = [];
        $('#checklist-items-container .checklist-item').each(function () {
            const $item = $(this);
            checklistItems.push({
                name: $item.attr('data-name'),
                description: $item.attr('data-desc'),
                isRequired: $item.attr('data-required') === 'true'
            });
        });

        // 5. Prepare Payload
        const parentTaskId = getParentTaskIdFromUrl();
        const createdBy = window.getUserName();
        const ownerId = window.getUserId();

        const payload = {
            id: currentEditingTaskId || null,
            parentTaskId: parentTaskId,
            createdBy: createdBy,
            ownerId: ownerId,
            name: name,
            description: description,
            statusId: parseInt(statusId),
            categoryId: parseInt(categoryId),
            priorityId: priorityId ? parseInt(priorityId) : null,
            startDate: startDateRaw || null,
            dueDate: dueDateRaw || null,
            estimatedTime: estimatedHour ? parseInt(estimatedHour) : null,
            assignees: assigneeList,
            followUpUserId: followUpUserId || null,
            followUpEstimatedTime: fuEstimatedHour ? parseInt(fuEstimatedHour) : null,
            dependencies: dependencies,
            checklistItems: checklistItems
        };

        console.log('Task payload ready:', payload);

        try {
            const response = await fetch(`${API.ppm}/Task/SaveTask`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            const result = await response.json();

            if (response.ok && !result?.errors) {
                showToast('Task saved successfully.', 'success');

                // Create ekranından çık
                $('[data-action="cancel-create"]').trigger('click');

                // Liste varsa reload et
                if (window.dtWorkstreamTask && $.fn.DataTable.isDataTable('.workstream-task-table')) {
                    const parentTaskId = getParentTaskIdFromUrl();
                    const workflowId = getWorkflowIdFromUrl();
                    const projectName = getProjectNameFromUrl();
                    const workstreamName = getWorkstreamNameFromUrl();

                    const newUrl =
                        `${API.ppm}/Workstream/GetTaskHierarchyByParentTask/${parentTaskId}` +
                        `? workflowId = ${workflowId} ` +
                        `& projectName=${encodeURIComponent(projectName)} ` +
                        `& workstreamName=${encodeURIComponent(workstreamName)} `;

                    dtWorkstreamTask
                        .ajax
                        .url(newUrl)
                        .load(null, false); // paging korunur
                } else if (!window.dtWorkstreamTask) {
                    initWorkstreamTaskTable();
                }
            } else {
                const errorMessage =
                    (Array.isArray(result?.errors) && result.errors.length > 0)
                        ? result.errors.join('<br/>')
                        : (result?.errors || 'Failed to save task.');

                showToast(errorMessage, 'error');
            }
        } catch (error) {
            console.error('SaveTask error:', error);
            showToast('An unexpected error occurred while saving the task.', 'error');
        }
    }


    $(document).on('click', '[data-action="save-task"]', function () {
        saveWorkstreamTask();
    });

    // Create Task
    $(document).on('click', '.create-task', function () {
        currentTaskMode = 'create';
        currentEditingTaskId = "";
        applyCreateModeActionButton();
        resetViewModeUiRules();

        // Reset Assign Multiple (Re-enable if it was disabled in Edit mode)
        const $amCheckbox = $('#assign-multiple');
        $amCheckbox.prop('disabled', false).removeAttr('data-bs-toggle').removeAttr('title');
        const amTooltip = bootstrap.Tooltip.getInstance($amCheckbox[0]);
        if (amTooltip) amTooltip.dispose();

        resetTaskUiRules();
        $mainContent.addClass('d-none');
        $createTaskContainer.removeClass('d-none');
        $createMeetingContainer.addClass('d-none');

        // Breadcrumb Update
        $breadcrumbActive.text('Create Task');

        // Load Dynamic Data
        loadAssignees();
        loadTaskCategories();
        loadTaskPriority();
        loadTaskStatus(false);
        loadDependencyTypes();
        loadDependencyDirections();
        loadDependencyReasons();
        loadDependencyTasks("");

        // Initialize components only once when first shown to ensure correct rendering (esp. Select2/Quill)
        if (!isTaskFormInitialized) {
            initCreateTaskComponents();
            isTaskFormInitialized = true;
        }
    });

    // Create Meeting
    $(document).on('click', '.create-meeting', function () {
        $mainContent.addClass('d-none');
        $createTaskContainer.addClass('d-none');
        $createMeetingContainer.removeClass('d-none');

        // Breadcrumb Update
        $breadcrumbActive.text('Create Meeting');

        // Reset Mode to Create
        setMeetingSaveButtonMode('create');
    });

    // Edit Record (Dynamic from DataTable)
    $(document).on('click', '.edit-record', async function () {
        const tr = $(this).closest('tr');
        if (!dtWorkstreamTask) return;

        const row = dtWorkstreamTask.row(tr);
        const data = row.data();
        if (!data) return;

        await openEditViewByType(data);
    });

    // View Record
    $(document).on('click', '.view-record', async function () {
        const tr = $(this).closest('tr');
        if (!dtWorkstreamTask) return;

        const row = dtWorkstreamTask.row(tr);
        const data = row.data();
        if (!data) return;

        await openViewViewByType(data);
    });

    // Cancel / Close
    $(document).on('click', '[data-action="cancel-create"]', function () {
        $mainContent.removeClass('d-none');
        $createTaskContainer.addClass('d-none');
        $createMeetingContainer.addClass('d-none');

        // Reset UI Rules (Important for next create/edit)
        resetViewModeUiRules();

        // Breadcrumb Reset
        resetBreadcrumb();

        // --- FORM RESET LOGIC ---
        const $form = $('#create-task-form');
        if ($form.length) {
            $form[0].reset();

            // 1. Reset Select2
            const selectIds = [
                '#task-assignee', '#task-category', '#task-priority', '#task-status',
                '#follow-up-user',
                '#dependency-task-select', '#dependency-type-select', '#dependency-direction-select', '#dependency-reason-select' // Dep Selects
            ];
            selectIds.forEach(id => {
                $(id).val(null).trigger('change');
            });
            // Reset "Assignee Multiple" toggle explicitly if needed or rely on form reset?
            $('#assign-multiple').prop('checked', false).trigger('change');


            // 2. Reset Quill
            if (window.taskDescriptionQuill) {
                window.taskDescriptionQuill.setText('');
            }

            // 3. Reset Flatpickr
            if (window.startDatePicker) window.startDatePicker.clear();
            if (window.dueDatePicker) window.dueDatePicker.clear();


            // 4. Dependencies List
            $('#dependencies-list').empty();

            // 5. Checklist Items
            $('#checklist-items-container').empty();

            // 6. Alerts
            $('#dependency-duplicate-alert').addClass('d-none');
            $('#multi-assign-alert').addClass('d-none');

        }
    });

    // --- VIEW ROUTING & HYDRATION ---

    async function openViewViewByType(data) {
        if (!data || !data.typeId) return;

        switch (Number(data.typeId)) {
            case 1:
                await openTaskView(data);
                break;
            case 2:
                openMeetingView(data);
                break;
            default:
                console.warn('Unknown TypeId:', data.typeId);
                break;
        }
    }

    async function openTaskView(data) {
        currentTaskMode = 'view';
        currentEditingTaskId = data.id;

        // Breadcrumb
        if (typeof pushTaskToPath === 'function') {
            pushTaskToPath(data.name, false);
        }
        $breadcrumbActive.text(data.name ? `View Task: ${data.name}` : 'View Task');

        resetViewModeUiRules(); // Clean slate first

        // Visibility
        $mainContent.addClass('d-none');
        $createTaskContainer.removeClass('d-none');
        $createMeetingContainer.addClass('d-none');

        if (!isTaskFormInitialized) {
            initCreateTaskComponents();
            isTaskFormInitialized = true;
        }

        // Populate fields
        populateTaskBaseFields(data);
        await populateTaskSelectFields(data);
        hydrateDependenciesForEdit(data.dependencies);
        populateChecklistItems(data.checklistItems);
        await loadDependencyTasks(data.id);

        // Apply Read-Only Rules
        applyViewModeUiRules();
    }

    function openMeetingView(data) {
        currentTaskMode = 'view';
        window.editingMeetingId = data.id;

        // Breadcrumb
        $breadcrumbActive.text(data.name ? `View Meeting: ${data.name}` : 'View Meeting');

        resetViewModeUiRules(); // Clean slate first

        // Visibility
        $mainContent.addClass('d-none');
        $createTaskContainer.addClass('d-none');
        $createMeetingContainer.removeClass('d-none');

        // Initialize meeting logic if needed
        if (typeof window.initCreateWorkstreamMeeting === 'function') {
            window.initCreateWorkstreamMeeting();
        }

        // Hydrate form
        populateMeetingFieldsForEdit(data);

        // Apply Read-Only Rules
        applyMeetingViewModeUiRules();
    }

    // --- EDIT ROUTING & HYDRATION ---

    function setMeetingSaveButtonMode(mode) {
        const $btn = $('[data-action="save-meeting"]');
        if (!$btn.length) return;

        if (mode === 'edit') {
            $btn.html('<i class="bx bx-save me-1"></i> Update Meeting');
        } else {
            $btn.html('<i class="bx bx-save me-1"></i> Save Meeting');
            window.editingMeetingId = null;
        }
    }

    async function openEditViewByType(data) {
        if (!data || !data.typeId) return;

        switch (Number(data.typeId)) {
            case 1:
                await openTaskEditView(data);
                break;
            case 2:
                openMeetingEditView(data);
                break;
            default:
                console.warn('Unknown TypeId:', data.typeId);
                break;
        }
    }

    async function openTaskEditView(data) {
        currentTaskMode = 'edit';
        resetViewModeUiRules();

        currentEditingTaskId = data.id;
        applyEditModeActionButton();

        // Update Breadcrumb (No Reload)
        if (typeof pushTaskToPath === 'function') {
            pushTaskToPath(data.name, false);
        }

        // Show Task Edit View
        $mainContent.addClass('d-none');
        $createTaskContainer.removeClass('d-none');
        $createMeetingContainer.addClass('d-none');

        $breadcrumbActive.text(data.name ? `Edit Task: ${data.name} ` : 'Edit Task');

        // Apply Follow-up UI rules
        data.isFollowUpTask ? applyFollowUpTaskUiRules() : resetTaskUiRules();

        // Ensure components initialized once
        if (!isTaskFormInitialized) {
            initCreateTaskComponents();
            isTaskFormInitialized = true;
        }

        // Populate static fields
        populateTaskBaseFields(data);

        // Populate async fields (order matters)
        await populateTaskSelectFields(data);

        // Hydrate Dependencies & Checklist
        hydrateDependenciesForEdit(data.dependencies);
        populateChecklistItems(data.checklistItems);

        // Load dependencies tasks list (for adding NEW ones)
        loadDependencyTasks(data.id);

        if (currentEditingTaskId) {
            applyEditModeAssigneeRules();
        }
    }

    function openMeetingEditView(data) {
        currentTaskMode = 'edit';
        window.editingMeetingId = data.id;

        // Set Mode to Edit
        setMeetingSaveButtonMode('edit');

        // Breadcrumb
        $breadcrumbActive.text(data.name ? `Edit Meeting: ${data.name}` : 'Edit Meeting');

        // Visibility
        $mainContent.addClass('d-none');
        $createTaskContainer.addClass('d-none');
        $createMeetingContainer.removeClass('d-none');

        // Initialize meeting logic if needed
        if (typeof window.initCreateWorkstreamMeeting === 'function') {
            window.initCreateWorkstreamMeeting();
        }

        // Hydrate form
        populateMeetingFieldsForEdit(data);
    }
    function parseIsoDateTime(isoString) {
        if (!isoString) return { date: null, time: null };

        const d = new Date(isoString);
        if (isNaN(d.getTime())) return { date: null, time: null };

        const date =
            d.getFullYear() +
            '-' +
            String(d.getMonth() + 1).padStart(2, '0') +
            '-' +
            String(d.getDate()).padStart(2, '0');

        const time =
            String(d.getHours()).padStart(2, '0') +
            ':' +
            String(d.getMinutes()).padStart(2, '0');

        return { date, time };
    }
    function populateMeetingFieldsForEdit(data) {
        if (!data) return;

        // Basics
        $('#meeting-title').val(data.name || '');
        $('#meeting-classification').val(data.categoryId || data.classificationId).trigger('change');
        $('#meeting-owner').val(data.ownerId).trigger('change');
        const attendeeIds = (data.assignees || []).map(x => x.id);
        $('#meeting-attendees').val(attendeeIds).trigger('change');
        // Dates & Times
        const start = parseIsoDateTime(data.startDate || data.startDateTime);
        const end = parseIsoDateTime(data.endDate || data.endDateTime);

        if (end.date && window.endDatePicker) {
            window.endDatePicker.setDate(end.date, true);
        }
        if (end.time) {
            $('#meeting-end-time').val(end.time).trigger('change');
        }

        if (start.date && window.startDatePicker) {
            window.startDatePicker.setDate(start.date, true);
        }
        if (start.time) {
            $('#meeting-start-time').val(start.time).trigger('change');
        }



        // Virtual Toggle
        const isVirtual = !!data.isVirtual;
        const $virtualToggle = $('#meeting-is-virtual');
        $virtualToggle.prop('checked', isVirtual).trigger('change');
        $virtualToggle.val(isVirtual).trigger('change');
        //$virtualToggle.prop('checked', isVirtual);
        //meetingToggleVirtual(); // 👈 manuel çağır

        const linkDiv = document.getElementById('container-meeting-link');
        const locDiv = document.getElementById('container-meeting-location');



        if (isVirtual) {
            $('#meeting-link').val(data.meetingLink || '');
            $('#meeting-location').val('');
            if (linkDiv) linkDiv.classList.remove('d-none');
            if (locDiv) locDiv.classList.add('d-none');
        } else {
            $('#meeting-location').val(data.location || '');
            $('#meeting-link').val('');
            if (linkDiv) linkDiv.classList.add('d-none');
            if (locDiv) locDiv.classList.remove('d-none');
        }

        // Notes (Quill)
        if (window.meetingNotesQuill) {
            window.meetingNotesQuill.clipboard.dangerouslyPasteHTML(data.description || '');
        }

        // Agenda Items
        hydrateAgendaItemsForEdit(data.agendaItems);
    }

    function hydrateAgendaItemsForEdit(agendaItems) {
        const $container = $('#agenda-items-container');
        $container.empty();

        if (!agendaItems || agendaItems.length === 0) {
            $container.html(`
                <div class="agenda-empty-state alert alert-primary alert-dismissible d-flex align-items-start gap-2">
                    <i class="bx bx-calendar fs-4 mt-1"></i>
                    <div>
                        <h5 class="alert-heading d-flex align-items-center flex-wrap gap-1 mb-1">No agenda items yet</h5>
                        <p class="mb-0"> Use <b>Add Item</b> to define the meeting flow and timings. </p>
                    </div>
                </div>
            `);
            return;
        }

        // Sort by order if available
        const sortedItems = [...agendaItems].sort((a, b) => (a.order || 0) - (b.order || 0));

        sortedItems.forEach(item => {
            if (typeof window.meetingAddAgendaItem === 'function') {
                window.meetingAddAgendaItem();
                const $row = $container.children('.agenda-row').last();

                $row.find('.agenda-title').val(item.title || '');

                // Attendees (Select2) - Expecting array of objects from backend
                const attendeeIds = (item.attendees || []).map(a => typeof a === 'object' ? a.id : a);
                if (attendeeIds.length > 0) {
                    $row.find('.agenda-attendees').val(attendeeIds).trigger('change');
                }

                // Parse ISO DateTimes
                const start = parseIsoDateTime(item.startDateTime);
                const end = parseIsoDateTime(item.endDateTime);

                // Start Date/Time
                $row.find('.agenda-start-date').val(start.date);
                $row.find('.agenda-start-time').val(start.time).trigger('change');

                // End Date (Flatpickr)
                if (end.date) {
                    const endInput = $row.find('.agenda-end-date')[0];
                    if (endInput && endInput._flatpickr) {
                        endInput._flatpickr.setDate(end.date, true);
                    } else {
                        $row.find('.agenda-end-date').val(end.date);
                    }
                }

                // End Time (Select2)
                if (end.time) {
                    $row.find('.agenda-end-time').val(end.time).trigger('change');
                }
            }
        });

        if (typeof window.meetingUpdateAgendaChain === 'function') {
            window.meetingUpdateAgendaChain();
        }
    }
}

/**
 * Initialize Meeting Form Logic (Agenda, Actions, Notes, Classification)
 */
function initMeetingFormLogic() {

    // --- State & Constants ---
    const $classificationSelect = $('#meeting-classification');
    const $agendaContainer = $('#agenda-items-container');
    const $actionContainer = $('#action-items-container');
    const $notesEditor = $('#meeting-notes-editor');

    // Notes Quill Instance
    let meetingNotesQuill = null;
    if ($notesEditor.length && !meetingNotesQuill) {
        meetingNotesQuill = new Quill('#meeting-notes-editor', {
            theme: 'snow',
            placeholder: 'Type meeting notes here...',
            modules: {
                toolbar: [
                    ['bold', 'italic', 'underline', 'strike'],
                    [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                    [{ 'header': [1, 2, 3, false] }],
                    ['clean']
                ]
            }
        });
    }

    // Classification Configs
    const classificationConfig = {
        // 4: Decision & Direction
        "4": {
            outputTypes: ["Decision", "Action", "Note"],
            notesTemplate: `
                <p><strong>Context</strong></p><p><br></p>
                <p><strong>Decision</strong></p><p><br></p>
                <p><strong>Rationale</strong></p><p><br></p>
                <p><strong>Impacted Areas</strong></p><p><br></p>
                <p><strong>Next Steps</strong></p><p><br></p>
            `
        },
        // 12: Retrospective
        "12": {
            outputTypes: ["Improvement", "Action", "Note"],
            notesTemplate: `
                <p><strong>What went well?</strong></p><ul><li><br></li></ul>
                <p><strong>What didn't go well?</strong></p><ul><li><br></li></ul>
                <p><strong>Root Causes</strong></p><p><br></p>
                <p><strong>Improvement Actions</strong></p><p><br></p>
            `
        },
        // 8: Financial & Commercial
        "8": {
            outputTypes: ["Decision", "Approval", "Action"],
            notesTemplate: `
                <p><strong>Budget Status</strong></p><p><br></p>
                <p><strong>Forecast Changes</strong></p><p><br></p>
                <p><strong>Commercial Decisions</strong></p><p><br></p>
                <p><strong>Approvals</strong></p><p><br></p>
            `
        },
        // 6: Risk & Issue (RAID)
        "6": { outputTypes: ["Risk", "Issue", "Mitigation", "Action", "Decision"] },
        // 5: Change Control
        "5": { outputTypes: ["Change Request", "Approval", "Communication", "Action"] },
        // Default
        "default": {
            outputTypes: ["Action", "Note", "Decision"],
            notesTemplate: `<p><strong>Meeting Minutes</strong></p><ul><li><br></li></ul>`
        }
    };

    function getClassificationData() {
        const val = $classificationSelect.val();
        return classificationConfig[val] || classificationConfig["default"];
    }

    function checkEmptyState(container, message = "No items added yet.") {
        if (container.children().length === 0) {
            container.html(`<div class="text-center text-muted py-4 empty-placeholder">${message}</div>`);
        } else {
            container.find('.empty-placeholder').remove();
        }
    }

    // --- 1. Classification Change Listener ---
    $classificationSelect.on('change', function () {
        const $selected = $(this).find('option:selected');
        const outputs = $selected.data('outputs');

        // 1. Update Expected Outputs Panel
        $('#agenda-outputs-text').text(outputs || 'Select a classification to see expected outputs.');

        // 2. Helper visibility
        if (outputs) {
            $('#classification-desc-helper').removeClass('d-none').text(`Outputs: ${outputs}`);
            $('#agenda-outputs-alert').removeClass('alert-secondary').addClass('alert-primary');
        } else {
            $('#classification-desc-helper').addClass('d-none');
            $('#agenda-outputs-alert').removeClass('alert-primary').addClass('alert-secondary');
        }
    });


    // --- 2. Agenda Tab Logic ---
    $('#add-agenda-item-btn').on('click', function () {
        const config = getClassificationData();
        const outputOptions = config.outputTypes.map(t => `<option value="${t}">${t}</option>`).join('');

        // Remove placeholder if exists
        $agendaContainer.find('.empty-placeholder').remove();

        const id = new Date().getTime(); // Simple ID

        const itemHtml = `
            <div class="card border p-3 agenda-item" id="agenda-item-${id}">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <h6 class="mb-0 fw-bold text-primary">Agenda Item</h6>
                    <button type="button" class="btn btn-sm btn-icon btn-label-danger remove-agenda-item">
                        <i class="bx bx-trash"></i>
                    </button>
                </div>
                <div class="row g-2">
                    <div class="col-md-8">
                        <label class="form-label small">Title</label>
                        <input type="text" class="form-control form-control-sm" placeholder="Discussion Topic">
                    </div>
                    <div class="col-md-4">
                        <label class="form-label small">Owner</label>
                        <select class="form-select form-select-sm">
                            <option value="">Select User</option>
                            <option value="me">Me</option>
                        </select>
                    </div>
                    <div class="col-12">
                        <label class="form-label small">Description</label>
                        <textarea class="form-control form-control-sm" rows="2" placeholder="Brief description..."></textarea>
                    </div>
                    <div class="col-md-6">
                        <label class="form-label small">Timebox (min)</label>
                        <input type="number" class="form-control form-control-sm" value="15" min="5" step="5">
                    </div>
                    <div class="col-md-6">
                        <label class="form-label small">Output Type</label>
                        <select class="form-select form-select-sm">
                            ${outputOptions}
                        </select>
                    </div>
                </div>
            </div>
        `;

        $agendaContainer.append(itemHtml);
    });

    // Remove Agenda Item
    $agendaContainer.on('click', '.remove-agenda-item', function () {
        $(this).closest('.agenda-item').remove();
        checkEmptyState($agendaContainer, "No agenda items added yet.");
    });


    // --- 3. Notes Tab Logic ---
    $('#btn-insert-notes-template').on('click', function () {
        if (!meetingNotesQuill) return;

        const config = getClassificationData();
        const template = config.notesTemplate || classificationConfig["default"].notesTemplate;

        const currentContent = meetingNotesQuill.getText().trim();

        if (currentContent.length > 0) {
            if (!confirm("This will overwrite existing notes. Continue?")) return;
        }

        meetingNotesQuill.clipboard.dangerouslyPasteHTML(template);
    });


    // --- 4. Actions Tab Logic ---
    $('#add-action-item-btn').on('click', function () {
        const classificationId = $classificationSelect.val(); // 1 = Delivery Operations
        const isDeliveryOps = (classificationId === "1");

        // Remove placeholder
        $actionContainer.find('.empty-placeholder').remove();

        const id = new Date().getTime();

        const itemHtml = `
            <div class="card border p-3 action-item" id="action-item-${id}">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <h6 class="mb-0 fw-bold text-success">Action Item</h6>
                    <button type="button" class="btn btn-sm btn-icon btn-label-danger remove-action-item">
                        <i class="bx bx-trash"></i>
                    </button>
                </div>
                <div class="row g-2">
                    <div class="col-md-8">
                        <label class="form-label small">Action Title</label>
                        <input type="text" class="form-control form-control-sm" placeholder="Do something...">
                    </div>
                    <div class="col-md-4">
                        <label class="form-label small">Status</label>
                        <select class="form-select form-select-sm">
                            <option value="pending">Pending</option>
                            <option value="in_progress">In Progress</option>
                            <option value="done">Done</option>
                        </select>
                    </div>
                    <div class="col-md-6">
                        <label class="form-label small">Owner</label>
                        <select class="form-select form-select-sm">
                            <option value="">Select User</option>
                            <option value="me">Me</option>
                        </select>
                    </div>
                    <div class="col-md-6">
                        <label class="form-label small ${isDeliveryOps ? 'text-danger fw-bold' : ''}">
                            Due Date ${isDeliveryOps ? '*' : ''}
                        </label>
                        <input type="date" class="form-control form-control-sm" ${isDeliveryOps ? 'required' : ''}>
                        ${isDeliveryOps ? '<div class="form-text text-danger" style="font-size:0.7rem;">Required for Delivery Ops</div>' : ''}
                    </div>
                    <div class="col-12 mt-2">
                         <div class="form-check">
                            <input class="form-check-input" type="checkbox" id="create-task-${id}">
                            <label class="form-check-label small" for="create-task-${id}"> Create as tracked Task</label>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $actionContainer.append(itemHtml);
    });

    // Remove Action Item
    $actionContainer.on('click', '.remove-action-item', function () {
        $(this).closest('.action-item').remove();
        checkEmptyState($actionContainer, "No action items yet.");
    });
}


// Dependencies required:
// - jQuery
// - Select2
// - Flatpickr
// These are loaded globally by layout.

(function () {
    'use strict';

    function meetingInitSelect2(selector, data, placeholder) {
        const $el = $(selector);
        if (!$el.length) return;

        // Destroy varsa temiz başla (çok önemli)
        if ($el.hasClass('select2-hidden-accessible')) {
            $el.select2('destroy');
            $el.empty();
        }

        // ✅ DOM option ekle (Select2 bunu sever)
        if (Array.isArray(data)) {
            data.forEach(item => {
                const option = new Option(item.text, item.id, false, false);
                $el.append(option);
            });
        }

        $el.select2({
            dropdownParent: $el.closest('.modal-body').length
                ? $el.closest('.modal-body')
                : ($el.closest('.modal-content').length
                    ? $el.closest('.modal-content')
                    : $(document.body)),
            placeholder: placeholder || '',
            allowClear: true,
            width: '100%',
            minimumResultsForSearch: 10
        });
    }

    function meetingInitOwnerSelect(users) {
        const ownerId = typeof window.getUserId === 'function'
            ? window.getUserId()
            : null;

        const $owner = $('#meeting-owner');
        if (!$owner.length) return;

        // Daha önce init edildiyse temizle
        if ($owner.hasClass('select2-hidden-accessible')) {
            $owner.select2('destroy');
            $owner.empty();
        }

        users.forEach(u => {
            const isMe = u.id == ownerId;
            const text = isMe
                ? `${u.fullName} (Me)`
                : u.fullName;

            const option = new Option(text, u.id, false, isMe);
            $owner.append(option);
        });

        $owner.select2({
            dropdownParent: $owner.closest('.modal-body').length
                ? $owner.closest('.modal-body')
                : ($owner.closest('.modal-content').length
                    ? $owner.closest('.modal-content')
                    : $(document.body)),
            placeholder: 'Select Owner',
            allowClear: false,
            width: '100%'
        });
    }

    function meetingValidateDateTime() {
        const startDateEl = document.querySelector('#meeting-start-date');
        const endDateEl = document.querySelector('#meeting-end-date');

        const startDateStr = startDateEl ? startDateEl.value : '';
        const endDateStr = endDateEl ? endDateEl.value : '';
        const startTimeStr = $('#meeting-start-time').val();
        const endTimeStr = $('#meeting-end-time').val();

        if (!startDateStr || !endDateStr) return;

        if (endDateStr < startDateStr) {
            showToast('End Date cannot be before Start Date.', 'error');
            if (window.endDatePicker) window.endDatePicker.clear();
            return;
        }

        if (startDateStr === endDateStr && startTimeStr && endTimeStr) {
            if (startTimeStr > endTimeStr) {
                showToast('Start Time cannot be later than End Time on the same day.', 'error');
                $('#meeting-end-time').val(null).trigger('change');
                return;
            }
            if (endTimeStr < startTimeStr) {
                showToast('End Time cannot be earlier than Start Time.', 'error');
                $('#meeting-end-time').val(null).trigger('change');
                return;
            }
        }
    }

    function meetingToggleVirtual() {
        const virtualToggle = document.getElementById('meeting-is-virtual');
        if (!virtualToggle) return;

        const isVirtual = virtualToggle.checked;
        const linkDiv = document.getElementById('container-meeting-link');
        const locDiv = document.getElementById('container-meeting-location');

        if (isVirtual) {
            if (linkDiv) linkDiv.classList.remove('d-none');
            if (locDiv) locDiv.classList.add('d-none');
        } else {
            if (linkDiv) linkDiv.classList.add('d-none');
            if (locDiv) locDiv.classList.remove('d-none');
        }
    }

    // Expose init function to call when partial is loaded or ready
    // Made idempotent to prevent double-init issues
    window.initCreateWorkstreamMeeting = function () {
        const form = document.getElementById('create-meeting-form');
        // If form is not in DOM, we can't init
        if (!form) return;

        // Prevent re-running if already done
        if (form.dataset.meetingInitialized === 'true') return;

        // --- 1. Init Times ---
        const timeOptions = [];
        for (let h = 0; h < 24; h++) {
            for (let m = 0; m < 60; m += 15) {
                const hh = h.toString().padStart(2, '0');
                const mm = m.toString().padStart(2, '0');
                timeOptions.push({ id: `${hh}:${mm}`, text: `${hh}:${mm}` });
            }
        }

        // Safety check for elements before init
        if (document.getElementById('meeting-start-time')) {
            meetingInitSelect2('#meeting-start-time', timeOptions, "Select start time");
        }
        if (document.getElementById('meeting-end-time')) {
            meetingInitSelect2('#meeting-end-time', timeOptions, "Select end time");
        }

        // --- 2. Init Classification (Dynamic from API, ID >= 11) ---
        const classificationEl = $('#meeting-classification');
        if (classificationEl.length && typeof API !== 'undefined') {
            const url = `${API.ppm}/Task/GetTaskCategory`;
            fetch(url)
                .then(res => res.json())
                .then(response => {
                    const allCats = Array.isArray(response) ? response : (response.data || []);

                    // Filter: ID >= 11 for Meetings
                    const meetingCats = allCats
                        .filter(c => Number(c.id) >= 11)
                        .map(c => ({ id: c.id, text: c.name }));

                    meetingInitSelect2('#meeting-classification', meetingCats, "Select Classification");
                })
                .catch(e => console.error("Failed to load meeting classifications", e));
        }

        // --- 3. Init Flatpickr ---
        const startDateEl = document.querySelector('#meeting-start-date');
        const endDateEl = document.querySelector('#meeting-end-date');
        const today = new Date();

        // Check if flatpickr already attached (._flatpickr property)
        if (startDateEl && !startDateEl._flatpickr) {
            window.startDatePicker = flatpickr(startDateEl, {
                dateFormat: 'Y-m-d',
                altInput: true,
                altFormat: 'd M Y',
                allowInput: true,
                minDate: 'today',
                defaultDate: today,
                static: true,
                onChange(selectedDates) {
                    if (selectedDates.length && window.endDatePicker) {
                        window.endDatePicker.set('minDate', selectedDates[0]);
                        meetingValidateDateTime();
                    }
                }
            });
        }

        if (endDateEl && !endDateEl._flatpickr) {
            window.endDatePicker = flatpickr(endDateEl, {
                dateFormat: 'Y-m-d',
                altInput: true,
                altFormat: 'd M Y',
                allowInput: true,
                minDate: 'today',
                static: true,
                onChange: function () { meetingValidateDateTime(); }
            });
        }

        // --- 4. Validation Listeners ---
        $('#meeting-start-time, #meeting-end-time').off('change', meetingValidateDateTime).on('change', meetingValidateDateTime);

        // --- 5. Virtual Toggle ---
        const virtualToggle = document.getElementById('meeting-is-virtual');
        if (virtualToggle) {
            virtualToggle.removeEventListener('change', meetingToggleVirtual); // avoid duplicate
            virtualToggle.addEventListener('change', meetingToggleVirtual);
            meetingToggleVirtual(); // Initial check
        }

        // --- 6. Attendees & Owner ---
        const attendeesEl = $('#meeting-attendees');
        if (attendeesEl.length && typeof API !== 'undefined') {
            const url = `${API.legacy.user}/api/PvUser/User/GetUsersByTenantId`;
            fetch(url)
                .then(res => res.json())
                .then(response => {

                    const users = Array.isArray(response)
                        ? response
                        : Array.isArray(response?.data)
                            ? response.data
                            : [];

                    // CACHE USERS for Agenda Use
                    window.meetingUsersCache = users;

                    if (Array.isArray(users)) {
                        const data = users.map(u => ({ id: u.UserId || u.id, text: u.fullName }));
                        meetingInitSelect2('#meeting-attendees', data, "Select Attendees");
                        meetingInitOwnerSelect(users);
                    } else {
                        console.warn('Attendees API returned non-array:', users);
                    }
                })
                .catch(e => console.error("Failed to load attendees", e));
        }

        // --- 7. Init Agenda Logic ---
        if (typeof window.meetingInitAgendaItems === 'function') {
            window.meetingInitAgendaItems();
        }

        // --- 8. Meeting Notes Quill Init ---
        // Global definition if not exists
        if (typeof window.meetingNotesQuill === 'undefined') {
            window.meetingNotesQuill = null;
        }

        const notesEditor = document.getElementById('meeting-notes-editor');
        if (notesEditor) {
            // Only init if not already initialized
            // check if the element has ql-container class is a standard check, or simply check the global variable
            // But if user cancels and re-opens, the DOM might be replaced? 
            // If DOM is replaced, the old instance references a detached node. 
            // So we should check if `notesEditor` has `.ql-editor`.
            if (!notesEditor.classList.contains('ql-container')) {
                const toolbarOptions = [
                    ['bold', 'italic', 'underline', 'strike'],        // toggled buttons
                    [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                    [{ 'color': [] }, { 'background': [] }],          // dropdown with defaults from theme
                    ['clean']                                         // remove formatting button
                ];

                window.meetingNotesQuill = new Quill(notesEditor, {
                    theme: 'snow',
                    placeholder: 'Meeting notes...',
                    modules: {
                        toolbar: toolbarOptions
                    }
                });
            }
        }

        // --- 9. Init Notes Logic ---
        if (typeof window.meetingInitNotesLogic === 'function') {
            window.meetingInitNotesLogic();
        }

        // Mark as initialized
        form.dataset.meetingInitialized = 'true';
    };

    // ============================================
    // NOTES TEMPLATE LOGIC
    // ============================================
    window.meetingInitNotesLogic = function () {
        const btnSelector = '#btn-insert-notes-template';
        const quillSelector = '#meeting-notes-editor';

        $(document).off('click', btnSelector).on('click', btnSelector, function () {
            const $classification = $('#meeting-classification');
            if (!$classification.length) return;

            const val = $classification.val();
            const classificationId = val ? Number(val) : null;

            // Templates
            const meetingNotesTemplates = {
                11: {
                    notesTemplate: `
                      <h3>🎯 Objective</h3>
                      <ul>
                        <li>Align on delivery progress vs plan</li>
                        <li>Identify blockers and assign owners</li>
                      </ul>

                      <h3>📊 Status Overview</h3>
                      <ul>
                        <li>Completed since last meeting</li>
                        <li>In-progress items</li>
                        <li>At-risk tasks</li>
                      </ul>

                      <h3>🚧 Blockers</h3>
                      <ul>
                        <li>Blocker description – Owner – Target date</li>
                      </ul>

                      <h3>✅ Actions</h3>
                      <ul>
                        <li>Action – Owner – Due date</li>
                      </ul>
                    `
                },

                12: {
                    notesTemplate: `
                      <h3>🔗 Dependencies Reviewed</h3>
                      <ul>
                        <li>Upstream dependencies</li>
                        <li>Downstream dependencies</li>
                      </ul>

                      <h3>🤝 Integration Points</h3>
                      <ul>
                        <li>System / Team</li>
                        <li>Expected input / output</li>
                      </ul>

                      <h3>⚠️ Risks</h3>
                      <ul>
                        <li>Dependency risk – Impact – Mitigation</li>
                      </ul>

                      <h3>✅ Actions</h3>
                      <ul>
                        <li>Action – Owner – Due date</li>
                      </ul>
                    `
                },

                13: {
                    notesTemplate: `
                      <h3>📌 Scope Clarification</h3>
                      <ul>
                        <li>In-scope items</li>
                        <li>Out-of-scope items</li>
                      </ul>

                      <h3>📝 Requirements</h3>
                      <ul>
                        <li>Confirmed requirements</li>
                        <li>Open questions</li>
                      </ul>

                      <h3>✔️ Acceptance Criteria</h3>
                      <ul>
                        <li>Criteria 1</li>
                        <li>Criteria 2</li>
                      </ul>
                    `
                },

                14: {
                    notesTemplate: `
                      <h3>🧭 Decisions Made</h3>
                      <ul>
                        <li>Decision – Rationale – Impact</li>
                      </ul>

                      <h3>📣 Communication</h3>
                      <ul>
                        <li>Who needs to be informed</li>
                      </ul>

                      <h3>✅ Actions</h3>
                      <ul>
                        <li>Action – Owner – Due date</li>
                      </ul>
                    `
                },

                15: {
                    notesTemplate: `
                      <h3>🔄 Change Requests</h3>
                      <ul>
                        <li>CR ID – Description</li>
                      </ul>

                      <h3>📐 Impact Analysis</h3>
                      <ul>
                        <li>Scope</li>
                        <li>Schedule</li>
                        <li>Cost</li>
                      </ul>

                      <h3>✔️ Decision</h3>
                      <ul>
                        <li>Approved / Rejected / Deferred</li>
                      </ul>
                    `
                },

                16: {
                    notesTemplate: `
                      <h3>⚠️ Risks</h3>
                      <ul>
                        <li>Risk – Probability – Impact</li>
                      </ul>

                      <h3>🔥 Issues</h3>
                      <ul>
                        <li>Issue – Owner – Resolution plan</li>
                      </ul>

                      <h3>🛠 Mitigation Actions</h3>
                      <ul>
                        <li>Action – Owner – Due date</li>
                      </ul>
                    `
                },

                17: {
                    notesTemplate: `
                      <h3>👥 Resource Allocation</h3>
                      <ul>
                        <li>Current capacity</li>
                        <li>Gaps</li>
                      </ul>

                      <h3>📅 Capacity Outlook</h3>
                      <ul>
                        <li>Next sprint / month forecast</li>
                      </ul>

                      <h3>✅ Actions</h3>
                      <ul>
                        <li>Staffing / vendor actions</li>
                      </ul>
                    `
                },

                18: {
                    notesTemplate: `
                      <h3>💰 Financial Overview</h3>
                      <ul>
                        <li>Budget status</li>
                        <li>Forecast</li>
                      </ul>

                      <h3>📄 Commercial Topics</h3>
                      <ul>
                        <li>POs</li>
                        <li>Invoices</li>
                        <li>Vendors</li>
                      </ul>

                      <h3>✔️ Decisions</h3>
                      <ul>
                        <li>Approval / escalation</li>
                      </ul>
                    `
                },

                19: {
                    notesTemplate: `
                      <h3>🧪 Quality Status</h3>
                      <ul>
                        <li>Validation status</li>
                        <li>Open findings</li>
                      </ul>

                      <h3>📋 Compliance</h3>
                      <ul>
                        <li>SOP adherence</li>
                        <li>Audit readiness</li>
                      </ul>

                      <h3>🛠 CAPA / Deviations</h3>
                      <ul>
                        <li>Action – Owner – Due date</li>
                      </ul>
                    `
                },

                20: {
                    notesTemplate: `
                      <h3>🛠 Workshop Goal</h3>
                      <ul>
                        <li>Target deliverable</li>
                      </ul>

                      <h3>🧩 Outputs Produced</h3>
                      <ul>
                        <li>Document / diagram / link</li>
                      </ul>

                      <h3>📌 Follow-ups</h3>
                      <ul>
                        <li>Next steps</li>
                      </ul>
                    `
                },

                21: {
                    notesTemplate: `
                      <h3>📊 Portfolio Overview</h3>
                      <ul>
                        <li>Initiatives reviewed</li>
                      </ul>

                      <h3>🧠 Strategic Decisions</h3>
                      <ul>
                        <li>Start / Stop / Continue</li>
                      </ul>

                      <h3>💼 Funding & Capacity</h3>
                      <ul>
                        <li>Reallocations</li>
                      </ul>
                    `
                },

                22: {
                    notesTemplate: `
                      <h3>🔍 What Went Well</h3>
                      <ul>
                        <li>Successes</li>
                      </ul>

                      <h3>⚠️ What Didn’t</h3>
                      <ul>
                        <li>Challenges</li>
                      </ul>

                      <h3>🚀 Improvements</h3>
                      <ul>
                        <li>Improvement – Owner – Due date</li>
                      </ul>
                    `
                },

                default: {
                    notesTemplate: `
                      <h3>📝 Meeting Notes</h3>
                      <ul>
                        <li>Discussion points</li>
                      </ul>
                    `
                }
            };

            // Select Template
            const config = meetingNotesTemplates[classificationId] || meetingNotesTemplates.default;
            const newContent = config.notesTemplate;

            // Check Quill
            if (!window.meetingNotesQuill) return;

            const currentContent = window.meetingNotesQuill.root.innerHTML;
            const isEffectivelyEmpty = currentContent === '<p><br></p>' || currentContent.trim() === '';

            if (isEffectivelyEmpty) {
                window.meetingNotesQuill.clipboard.dangerouslyPasteHTML(newContent);
            } else {
                // CONFIRMATION REQUIRED
                if (typeof Swal !== 'undefined') {
                    Swal.fire({
                        title: 'Replace existing notes?',
                        text: "Current notes will be replaced with a predefined template.",
                        icon: 'warning',
                        showCancelButton: true,
                        confirmButtonColor: '#3085d6',
                        cancelButtonColor: '#d33',
                        confirmButtonText: 'Replace Notes'
                    }).then((result) => {
                        if (result.isConfirmed) {
                            window.meetingNotesQuill.clipboard.dangerouslyPasteHTML(newContent);
                        }
                    });
                } else {
                    // Fallback: Bootstrap Modal manually
                    const modalId = 'confirm-notes-replace-modal';
                    if (!$('#' + modalId).length) {
                        $('body').append(`
                            <div class="modal fade" id="${modalId}" tabindex="-1" aria-hidden="true">
                              <div class="modal-dialog modal-dialog-centered">
                                <div class="modal-content">
                                  <div class="modal-header">
                                    <h5 class="modal-title">Replace existing notes?</h5>
                                    <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                                  </div>
                                  <div class="modal-body">
                                    Current notes will be replaced with a predefined template.
                                  </div>
                                  <div class="modal-footer">
                                    <button type="button" class="btn btn-label-secondary" data-bs-dismiss="modal">Cancel</button>
                                    <button type="button" class="btn btn-primary" id="${modalId}-confirm">Replace Notes</button>
                                  </div>
                                </div>
                              </div>
                            </div>
                         `);
                    }

                    const $modal = $('#' + modalId);
                    const modal = new bootstrap.Modal($modal[0]);
                    modal.show();

                    $modal.find(`#${modalId}-confirm`).off('click').on('click', function () {
                        window.meetingNotesQuill.clipboard.dangerouslyPasteHTML(newContent);
                        modal.hide();
                    });
                }
            }
        });
    };

    // ============================================
    // AGENDA LOGIC
    // ============================================
    window.meetingInitAgendaItems = function () {
        const $container = $('#agenda-items-container');
        const $addBtn = $('#add-agenda-item-btn');

        if (!$container.length || !$addBtn.length) return;

        // Ensure clean events
        $addBtn.off('click').on('click', addAgendaItem);
        $container.off('click', '.agenda-remove').on('click', '.agenda-remove', removeAgendaItem);

        // Chain Updates: When an End Date/Time changes, update next item's Start
        $container.off('change', '.agenda-end-date, .agenda-end-time').on('change', '.agenda-end-date, .agenda-end-time', function () {
            // Re-validate this item
            const $row = $(this).closest('.agenda-row');
            validateAgendaRow($row);
            // Update chain
            updateAgendaChain();
        });

        // Also validate start/end changes for the current row
        $container.off('change', '.agenda-start-date, .agenda-start-time').on('change', '.agenda-start-date, .agenda-start-time', function () {
            const $row = $(this).closest('.agenda-row');
            validateAgendaRow($row);
        });

        // Meeting Time Change -> Re-validate all (optional but good)
        $('#meeting-start-date, #meeting-start-time, #meeting-end-date, #meeting-end-time').on('change', function () {
            updateAgendaChain(); // Re-sync first item and bounds
        });

        // Auto-create initial agenda item on first tab visit
        $('button[data-bs-target="#tab-meeting-agenda"]').on('shown.bs.tab', function () {
            const form = document.getElementById('create-meeting-form');
            if (!form) return;

            // Guard: Only run once per session
            if (form.dataset.initialAgendaCreated === 'true') return;

            // Guard: Only if no items exist (safety)
            if ($container.children('.agenda-row').length > 0) return;

            createInitialAgendaItemFromMeeting();
            form.dataset.initialAgendaCreated = 'true';
        });

        function createInitialAgendaItemFromMeeting() {
            // 1. Create clean item
            addAgendaItem();

            // 2. Get the new row (it should be the last one)
            const $row = $container.children('.agenda-row').last();
            if (!$row.length) return;

            // 3. Populate from Meeting
            const title = $('#meeting-title').val();
            const attendees = $('#meeting-attendees').val(); // array of IDs
            const endD = $('#meeting-end-date').val();
            const endT = $('#meeting-end-time').val();

            // Title
            if (title) $row.find('.agenda-title').val(title);

            // Attendees
            if (attendees && attendees.length) {
                // Since options are already loaded in initAgendaComponents via cache, we can just set val
                $row.find('.agenda-attendees').val(attendees).trigger('change');
            }

            // End Date (Flatpickr)
            if (endD) {
                const endInput = $row.find('.agenda-end-date')[0];
                if (endInput && endInput._flatpickr) {
                    endInput._flatpickr.setDate(endD, true);
                } else {
                    $row.find('.agenda-end-date').val(endD);
                }
            }

            // End Time (Select2)
            if (endT) {
                $row.find('.agenda-end-time').val(endT).trigger('change');
            }

            // Re-eval chain
            updateAgendaChain();
        }

        function addAgendaItem() {
            // Check empty state
            if ($container.children('.agenda-empty-state').length) {
                $container.empty();
            }

            const index = $container.children('.agenda-row').length + 1;
            const prevItem = $container.children('.agenda-row').last();

            // Calculate Start
            let startDate = '';
            let startTime = '';

            if (prevItem.length) {
                // Continuation
                startDate = prevItem.find('.agenda-end-date').val();
                startTime = prevItem.find('.agenda-end-time').val();
            } else {
                // First Item = Meeting Start
                startDate = $('#meeting-start-date').val();
                startTime = $('#meeting-start-time').val();
            }

            // Generate HTML (Timeline Style + Clean Dates)
            const html = `
                <div class="agenda-row d-flex align-items-start gap-3 p-2 rounded hover-bg" data-index="${index}">
                  <!-- Step Number -->
                  <div class="agenda-step pt-2">
                    <span class="badge rounded-circle bg-label-primary px-3 py-2 fs-5 step-number">${index}</span>
                  </div>

                  <!-- Content -->
                  <div class="agenda-content flex-grow-1">
                    <!-- Title + Attendees -->
                    <div class="d-flex gap-2 mb-2">
                      <div class="flex-grow-1">
                           <input type="text" class="form-control agenda-title" placeholder="Agenda item title" required>
                      </div>
                      <div class="w-50">
                           <select class="form-select agenda-attendees" multiple data-placeholder="Attendees"></select>
                      </div>
                    </div>

                    <!-- Time Row (Grid Layout) -->
                    <div class="row g-2 align-items-end agenda-time-row">
                      <div class="col-md-3 col-sm-6">
                        <label class="form-label small mb-1">Start Date</label>
                        <input type="text" class="form-control agenda-start-date" value="${startDate || ''}" readonly>
                      </div>

                      <div class="col-md-2 col-sm-6">
                        <label class="form-label small mb-1">Start</label>
                        <select class="form-select agenda-start-time"></select>
                      </div>

                      <div class="col-md-1 d-none d-md-flex justify-content-center align-items-center pb-1">
                        <i class="bx bx-right-arrow-alt text-muted fs-4"></i>
                      </div>

                      <div class="col-md-3 col-sm-6">
                        <label class="form-label small mb-1">End Date</label>
                        <input type="text" class="form-control agenda-end-date" value="${startDate || ''}">
                      </div>

                      <div class="col-md-2 col-sm-6">
                        <label class="form-label small mb-1">End</label>
                        <select class="form-select agenda-end-time"></select>
                      </div>
                    </div>
                  </div>

                  <!-- Remove -->
                  <div class="agenda-actions pt-2">
                    <button type="button" class="btn btn-icon btn-label-danger agenda-remove">
                      <i class="bx bx-trash"></i>
                    </button>
                  </div>
                </div>
            `;

            const $el = $(html);
            $container.append($el);

            // Init Components
            initAgendaComponents($el, startTime);
            updateAgendaChain(); // Re-evaluate all items after adding
        }

        function initAgendaComponents($row, startTimeVal) {
            // 1. Time Selects (Same options as Meeting Details)
            const timeOptions = [];
            for (let h = 0; h < 24; h++) {
                for (let m = 0; m < 60; m += 15) {
                    const hh = h.toString().padStart(2, '0');
                    const mm = m.toString().padStart(2, '0');
                    timeOptions.push({ id: `${hh}:${mm}`, text: `${hh}:${mm}` });
                }
            }

            const $startSelect = $row.find('.agenda-start-time');
            const $endSelect = $row.find('.agenda-end-time');

            meetingInitSelect2($startSelect, timeOptions, "");
            meetingInitSelect2($endSelect, timeOptions, "");

            // Set Start Time
            if (startTimeVal) {
                $startSelect.val(startTimeVal).trigger('change');
                // Default End = Start (User sets End)
                $endSelect.val(startTimeVal).trigger('change');
            }

            // Disable Start Select (Chained)
            $startSelect.prop('disabled', true);

            // 2. Dates
            // START DATE: Do NOT init Flatpickr (Chained / Readonly)
            // END DATE: Init Flatpickr (Interactive)
            const agendaStartDate = $row.find('.agenda-start-date').val();
            const meetingEndDate = $('#meeting-end-date').val();
            const $endDate = $row.find('.agenda-end-date');

            if (typeof flatpickr !== 'undefined') {
                flatpickr($endDate[0], {
                    dateFormat: 'Y-m-d',
                    altInput: true,
                    altFormat: 'd M Y',
                    allowInput: true,
                    disableMobile: true,
                    static: true,
                    minDate: agendaStartDate || 'today',
                    maxDate: meetingEndDate || null
                });
            }

            // 3. Attendees
            const $att = $row.find('.agenda-attendees');
            if (window.meetingUsersCache && Array.isArray(window.meetingUsersCache)) {
                const data = window.meetingUsersCache.map(u => ({ id: u.UserId || u.id, text: u.fullName }));
                meetingInitSelect2($att, data, "Select");
            }
        }

        function removeAgendaItem() {
            $(this).closest('.agenda-row').remove();
            if ($container.children('.agenda-row').length === 0) {
                $container.html(`
                    <div class="agenda-empty-state alert alert-info d-flex align-items-start gap-2">
                        <i class="bx bx-calendar fs-4 mt-1"></i>
                        <div>
                            <strong>No agenda items yet</strong>
                            <div class="small">
                              Use <b>Add Item</b> to define the meeting flow and timings.
                            </div>
                        </div>
                    </div>
                `);
            } else {
                updateStepNumbers();
                updateAgendaChain();
            }
        }

        function updateStepNumbers() {
            $container.children('.agenda-row').each(function (index) {
                $(this).find('.step-number').text(index + 1);
                $(this).attr('data-index', index + 1);
            });
        }

        function updateAgendaChain() {
            const items = $container.children('.agenda-row');

            // Bounds
            const meetingStartD = $('#meeting-start-date').val();
            const meetingStartT = $('#meeting-start-time').val();
            const meetingEndD = $('#meeting-end-date').val();
            const meetingEndT = $('#meeting-end-time').val();

            let currentStartD = meetingStartD;
            let currentStartT = meetingStartT;

            items.each(function (idx) {
                const $row = $(this);
                const $startD = $row.find('.agenda-start-date');
                const $startT = $row.find('.agenda-start-time');
                const $endD = $row.find('.agenda-end-date');
                const $endT = $row.find('.agenda-end-time');

                // Set Start of This Item = Current Tracker
                // Note: Without flatpickr on startD, this just sets the value.
                $startD.val(currentStartD);
                $startT.val(currentStartT).trigger('change.select2');

                // Update Min Date for End Date Flatpickr if it exists
                if ($endD[0]._flatpickr) {
                    $endD[0]._flatpickr.set('minDate', currentStartD);
                    if (meetingEndD) $endD[0]._flatpickr.set('maxDate', meetingEndD);
                }

                // Validate Row (End vs Start)
                // We read the 'Y-m-d' value from the flatpickr input (or standard value attribute)
                let endD = $endD.val();
                let endT = $endT.val();

                // If current item's end is before its start, or before meeting start, reset/propagate
                if (!endD || !endT || (endD < currentStartD) || (endD === currentStartD && endT < currentStartT)) {
                    endD = currentStartD;
                    endT = currentStartT;
                }

                // Check against meeting end
                if (meetingEndD && meetingEndT) {
                    if (endD > meetingEndD || (endD === meetingEndD && endT > meetingEndT)) {
                        endD = meetingEndD;
                        endT = meetingEndT;
                    }
                }

                // Propagate to next item's start
                currentStartD = endD;
                currentStartT = endT;
            });
        }

        function validateAgendaRow($row) {
            const startD = $row.find('.agenda-start-date').val();
            const startT = $row.find('.agenda-start-time').val();
            const endD = $row.find('.agenda-end-date').val();
            const endT = $row.find('.agenda-end-time').val();

            // Clear previous validation messages
            $row.find('.is-invalid').removeClass('is-invalid');
            $row.find('.invalid-feedback').remove();

            if (!endD || !endT) {
                // If end is empty, don't validate time comparison yet, but mark required if desired
                if (!endD) $row.find('.agenda-end-date').addClass('is-invalid');
                if (!endT) $row.find('.agenda-end-time').addClass('is-invalid');
                return;
            }

            let isValid = true;
            if (endD < startD || (endD === startD && endT <= startT)) {
                $row.find('.agenda-end-date, .agenda-end-time').addClass('is-invalid');
                isValid = false;
            }

            // Allow bounds checking against Meeting End here if needed
            const meetingEndD = $('#meeting-end-date').val();
            const meetingEndTime = $('#meeting-end-time').val();
            if (meetingEndD && meetingEndTime) {
                if (endD > meetingEndD || (endD === meetingEndD && endT > meetingEndTime)) {
                    $row.find('.agenda-end-date, .agenda-end-time').addClass('is-invalid');
                    isValid = false;
                }
            }
            return isValid;
        }

        // Expose internally for hydration
        window.meetingAddAgendaItem = addAgendaItem;
        window.meetingUpdateAgendaChain = updateAgendaChain;
    };

    // ============================================
    // TIMELINE LOGIC
    // ============================================

    const timelineStyles = `
        .vertical-timeline { position: relative; padding: 10px 0; }
        .vertical-timeline::before { content: ''; position: absolute; top: 0; bottom: 0; left: 15px; width: 2px; background: #eaedf1; }
        .timeline-item { position: relative; margin-bottom: 20px; transition: all 0.2s ease; }
        .timeline-dot { position: absolute; left: 0; top: 8px; width: 14px; height: 14px; border-radius: 50%; background: #fff; border: 3px solid #82868b; z-index: 2; box-shadow: 0 0 0 4px #fff; }
        .timeline-item.meeting .timeline-dot { border-color: #7367f0; }
        .timeline-item.task .timeline-dot { border-color: #82868b; }
        .timeline-item.completed .timeline-dot { border-color: #28c76f; background: #28c76f; }
        
        .timeline-content { padding-left: 30px; }
        .timeline-content .card { border: 1px solid #f0f2f4; transition: transform 0.2s, box-shadow 0.2s; background: #fff; cursor: default; }
        .timeline-item.has-children .card { cursor: pointer; }
        .timeline-content .card:hover { transform: translateX(5px); box-shadow: 0 4px 12px rgba(0,0,0,0.05); }
        .timeline-agenda { border-radius: 6px; background: rgba(115, 103, 240, 0.04); border-left: 3px solid #7367f0; }
        .timeline-children { position: relative; transition: all 0.3s ease-in-out; overflow: hidden; }
        .timeline-children::before { content: ''; position: absolute; top: 0; bottom: 0; left: 15px; width: 2px; background: #eaedf1; }
        
        .timeline-chevron { transition: transform 0.2s; display: inline-block; cursor: pointer; color: #82868b; margin-right: 5px; }
        .timeline-chevron:hover { color: #7367f0; }
        .timeline-item.collapsed .timeline-chevron { transform: rotate(-90deg); }
        .timeline-item.collapsed + .timeline-children { max-height: 0 !important; margin-bottom: 0 !important; visibility: hidden; opacity: 0; }
        
        .timeline-filter-bar { background: #f8f9fa; border-radius: 8px; border: 1px solid #e9ecef; }
        .child-count-badge { font-size: 0.7rem; opacity: 0.7; vertical-align: middle; }

        @media print {
            body * { visibility: hidden; }
            #timeline, #timeline * { visibility: visible; }
            #timeline { position: absolute; left: 0; top: 0; width: 100%; }
            .timeline-filter-bar, .timeline-chevron, .btn { display: none !important; }
            .card { border: 1px solid #ddd !important; box-shadow: none !important; transform: none !important; }
            .timeline-dot { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
            .badge { border: 1px solid #ccc !important; color: #000 !important; -webkit-print-color-adjust: exact; }
        }
    `;

    function injectTimelineStyles() {
        if ($('#timeline-styles').length) return;
        $('<style id="timeline-styles">').text(timelineStyles).appendTo('head');
    }

    window.initTimelineTab = function () {
        const $timelinePane = $('#timeline');
        const $timelineTabBtn = $('button[data-bs-target="#timeline"]');

        if (!$timelinePane.length || !$timelineTabBtn.length) return;

        // === TIMELINE STATE ===
        window.timelineRawData = null;
        window.timelineFilters = { type: 'all', status: 'all', virtual: 'all', assignee: 'all' };
        window.timelineItemStates = new Map(); // Store collapsed IDs

        $timelineTabBtn.off('shown.bs.tab').on('shown.bs.tab', async function () {
            if ($timelinePane.find('.vertical-timeline').length > 0) return;

            const parentTaskId = getParentTaskIdFromUrl();
            if (!parentTaskId) {
                $timelinePane.html('<div class="text-center p-5 text-muted"><i class="bx bx-info-circle fs-2 d-block mb-2"></i>No parent task ID found in URL.</div>');
                return;
            }

            injectTimelineStyles();
            $timelinePane.html('<div class="text-center p-5"><div class="spinner-border text-primary" role="status"></div></div>');

            try {
                const response = await fetch(`${API.ppm}/Workstream/GetTaskHierarchyByParentTask/${parentTaskId}`);
                if (!response.ok) throw new Error("API failed");
                const result = await response.json();

                window.timelineRawData = result.data; // Store original

                $timelinePane.empty();
                renderTimelineFiltersUI();
                renderTimelineTab(window.timelineRawData);

            } catch (err) {
                console.error("Timeline error:", err);
                $timelinePane.html('<div class="text-center p-5 text-danger">Error loading timeline.</div>');
            }
        });

        // Click delegation for toggling
        $(document).off('click', '.timeline-item.has-children .card, .timeline-chevron').on('click', '.timeline-item.has-children .card, .timeline-chevron', function (e) {
            e.stopPropagation();
            const $item = $(this).closest('.timeline-item');
            const taskId = $item.data('task-id');
            window.toggleTimelineNode(taskId, $item);
        });
    };

    // === ASSIGNEE FILTER START ===
    function extractDistinctAssignees(data) {
        const users = new Map();
        const traverse = (item) => {
            if (!item) return;
            if (item.ownerId && item.ownerName) users.set(String(item.ownerId), item.ownerName);
            (item.assignees || []).forEach(a => { if (a.id && a.name) users.set(String(a.id), a.name); });
            (item.agendaItems || []).forEach(ag => {
                (ag.attendees || []).forEach(att => {
                    if (att && att.id && att.name) users.set(String(att.id), att.name);
                });
            });
            (item.subTasks || []).forEach(traverse);
        };
        const root = Array.isArray(data) ? data : [data];
        root.forEach(traverse);
        return Array.from(users.entries()).map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
    }

    function renderTimelineFiltersUI() {
        const $container = $('#timeline');
        if ($('#timeline-filter-wrapper').length) return;

        const assignees = extractDistinctAssignees(window.timelineRawData);
        const assigneeOptions = assignees.map(u => `<option value="${u.id}">${u.name}</option>`).join('');

        const html = `
            <div id="timeline-filter-wrapper" class="timeline-filter-bar p-3 mb-4 d-flex flex-wrap gap-3 align-items-center shadow-sm">
                <div class="filter-group">
                    <label class="small fw-bold text-muted d-block mb-1">TYPE</label>
                    <div class="btn-group btn-group-sm" role="group">
                        <input type="radio" class="btn-check" name="tl-type" id="tl-type-all" value="all" checked>
                        <label class="btn btn-outline-primary" for="tl-type-all">All</label>
                        <input type="radio" class="btn-check" name="tl-type" id="tl-type-task" value="1">
                        <label class="btn btn-outline-primary" for="tl-type-task">Tasks</label>
                        <input type="radio" class="btn-check" name="tl-type" id="tl-type-meeting" value="2">
                        <label class="btn btn-outline-primary" for="tl-type-meeting">Meetings</label>
                    </div>
                </div>
                <div class="filter-group">
                    <label class="small fw-bold text-muted d-block mb-1">STATUS</label>
                    <div class="btn-group btn-group-sm" role="group">
                        <input type="radio" class="btn-check" name="tl-status" id="tl-status-all" value="all" checked>
                        <label class="btn btn-outline-primary" for="tl-status-all">All</label>
                        <input type="radio" class="btn-check" name="tl-status" id="tl-status-active" value="active">
                        <label class="btn btn-outline-primary" for="tl-status-active">Active</label>
                        <input type="radio" class="btn-check" name="tl-status" id="tl-status-done" value="done">
                        <label class="btn btn-outline-primary" for="tl-status-done">Completed</label>
                    </div>
                </div>
                <div class="filter-group">
                    <label class="small fw-bold text-muted d-block mb-1">ASSIGNEE</label>
                    <select id="tl-assignee-filter" class="form-select form-select-sm" style="min-width: 150px;">
                        <option value="all">All Assignees</option>
                        ${assigneeOptions}
                    </select>
                </div>
                <div class="filter-group" id="tl-virtual-group">
                    <label class="small fw-bold text-muted d-block mb-1">LOCATION</label>
                    <div class="btn-group btn-group-sm" role="group">
                        <input type="radio" class="btn-check" name="tl-virt" id="tl-virt-all" value="all" checked>
                        <label class="btn btn-outline-primary" for="tl-virt-all">All</label>
                        <input type="radio" class="btn-check" name="tl-virt" id="tl-virt-v" value="true">
                        <label class="btn btn-outline-primary" for="tl-virt-v">Virtual</label>
                        <input type="radio" class="btn-check" name="tl-virt" id="tl-virt-p" value="false">
                        <label class="btn btn-outline-primary" for="tl-virt-p">Physical</label>
                    </div>
                </div>
                <div class="ms-auto d-flex gap-2">
                    <button type="button" id="btn-timeline-print" class="btn btn-sm btn-outline-secondary"><i class="bx bx-printer me-1"></i>Print</button>
                    <button type="button" id="btn-timeline-excel" class="btn btn-sm btn-outline-secondary"><i class="bx bx-download me-1"></i>Excel</button>
                </div>
            </div>
        `;
        $container.prepend(html);

        // Events
        $('#timeline-filter-wrapper input, #tl-assignee-filter').on('change', function () {
            window.timelineFilters.type = $('input[name="tl-type"]:checked').val();
            window.timelineFilters.status = $('input[name="tl-status"]:checked').val();
            window.timelineFilters.virtual = $('input[name="tl-virt"]:checked').val();
            window.timelineFilters.assignee = $('#tl-assignee-filter').val();

            if (window.timelineFilters.type === '1') $('#tl-virtual-group').addClass('opacity-50 pointer-events-none');
            else $('#tl-virtual-group').removeClass('opacity-50 pointer-events-none');

            renderTimelineTab(window.timelineRawData);
        });

        $('#btn-timeline-print').on('click', function () {
            printTimeline();
        });

        $('#btn-timeline-excel').on('click', function () {
            exportTimelineToExcel();
        });
    }

    function applyTimelineFilters(data) {
        if (!data) return null;
        const f = window.timelineFilters;

        const filterRecursive = (item) => {
            const isCompleted = (item.statusId === 3) || (item.statusName && item.statusName.toLowerCase() === 'completed');
            const filteredSubs = (item.subTasks || []).map(filterRecursive).filter(Boolean);

            let matches = true;
            if (f.type !== 'all' && String(item.typeId) !== f.type) matches = false;
            if (f.status === 'active' && isCompleted) matches = false;
            if (f.status === 'done' && !isCompleted) matches = false;
            if (item.typeId == 2 && f.virtual !== 'all') {
                if (String(item.isVirtual) !== f.virtual) matches = false;
            }

            // Assignee Match Logic
            if (f.assignee !== 'all') {
                const uId = String(f.assignee);
                const hasMatch = (String(item.ownerId) === uId) ||
                    (item.assignees || []).some(a => String(a.id) === uId) ||
                    (item.agendaItems || []).some(ag => (ag.attendees || []).some(att => (typeof att === 'object' ? String(att.id) : String(att)) === uId));

                if (!hasMatch) matches = false;
            }

            if (matches || filteredSubs.length > 0) {
                return { ...item, subTasks: filteredSubs };
            }
            return null;
        };

        if (Array.isArray(data)) return data.map(filterRecursive).filter(Boolean);
        return filterRecursive(data);
    }
    // === ASSIGNEE FILTER END ===

    // === COLLAPSE FIX START ===
    window.toggleTimelineNode = function (taskId, $item) {
        if (!taskId) return;
        const $children = $item.next('.timeline-children');
        const isCollapsed = window.timelineItemStates.has(taskId);

        if (isCollapsed) {
            window.timelineItemStates.delete(taskId);
            $item.removeClass('collapsed');
            $children.stop(true, true).slideDown(250);
        } else {
            window.timelineItemStates.set(taskId, true);
            $item.addClass('collapsed');
            $children.stop(true, true).slideUp(250);
        }
    };
    // === TIMELINE PRINT START ===
    function printTimeline() {
        const $nodes = $('.timeline-item.has-children.collapsed');
        // Temporarily expand all
        $nodes.each(function () {
            const taskId = $(this).data('task-id');
            const $children = $(this).next('.timeline-children');
            $(this).removeClass('collapsed');
            $children.show();
        });

        setTimeout(() => {
            window.print();
            // Restore state
            $nodes.each(function () {
                const taskId = $(this).data('task-id');
                if (window.timelineItemStates.has(taskId)) {
                    $(this).addClass('collapsed');
                    $(this).next('.timeline-children').hide();
                }
            });
        }, 500);
    }
    // === TIMELINE PRINT END ===

    // === TIMELINE EXCEL EXPORT START ===
    function exportTimelineToExcel() {
        const filteredData = applyTimelineFilters(window.timelineRawData);
        if (!filteredData) return;

        const rows = [];
        const items = Array.isArray(filteredData) ? filteredData : [filteredData];

        const flatten = (item, level, parentName) => {
            const isMeeting = Number(item.typeId) === 2;
            const assignees = (item.assignees || []).map(a => a.name).join(', ');

            rows.push({
                Level: level,
                Type: isMeeting ? 'Meeting' : 'Task',
                Name: item.name || item.title || '',
                Status: item.statusName || '',
                Owner: item.ownerName || '',
                Start: item.startDate || item.startDateTime || '',
                End: item.endDate || item.endDateTime || '',
                Virtual: isMeeting ? (item.isVirtual ? 'Yes' : 'No') : '-',
                Assignees: assignees,
                Parent: parentName || ''
            });

            if (item.subTasks && item.subTasks.length > 0) {
                item.subTasks.forEach(child => flatten(child, level + 1, item.name || item.title));
            }
        };

        items.forEach(item => flatten(item, 0, ''));

        if (typeof XLSX !== 'undefined') {
            const worksheet = XLSX.utils.json_to_sheet(rows);
            const workbook = XLSX.utils.book_new();
            XLSX.utils.book_append_sheet(workbook, worksheet, "Timeline");
            XLSX.writeFile(workbook, `Workstream_Timeline_${moment().format('YYYYMMDD')}.xlsx`);
        } else {
            // Fallback to CSV
            let csvContent = "data:text/csv;charset=utf-8," + Object.keys(rows[0]).join(",") + "\n";
            rows.forEach(r => {
                csvContent += Object.values(r).map(v => `"${v}"`).join(",") + "\n";
            });
            const encodedUri = encodeURI(csvContent);
            const link = document.createElement("a");
            link.setAttribute("href", encodedUri);
            link.setAttribute("download", `Workstream_Timeline_${moment().format('YYYYMMDD')}.csv`);
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        }
    }
    // === TIMELINE EXCEL EXPORT END ===

    // === DEPENDENCIES TAB JS START ===
    window.initDependenciesTab = function () {
        const $depPane = $('#dependency-tab-content');
        const $depTabBtn = $('button[data-bs-target="#dependencies"]');
        if (!$depPane.length || !$depTabBtn.length) return;

        $depTabBtn.off('shown.bs.tab').on('shown.bs.tab', async function () {
            // Already loaded check
            if ($depPane.find('.dependency-group').length > 0) return;

            const parentTaskId = getParentTaskIdFromUrl();
            if (!parentTaskId) {
                $depPane.html('<div class="text-center p-5 text-muted">No parent task ID found.</div>');
                return;
            }

            try {
                let data = window.timelineRawData;
                if (!data) {
                    const response = await fetch(`${API.ppm}/Workstream/GetTaskHierarchyByParentTask/${parentTaskId}`);
                    if (!response.ok) throw new Error("API failed");
                    const result = await response.json();
                    data = result.data;
                    window.timelineRawData = data; // Cache it
                }

                renderDependenciesTab(data);
            } catch (err) {
                console.error("Dependency tab error:", err);
                $depPane.html('<div class="text-center p-5 text-danger">Error loading dependencies.</div>');
            }
        });

        // Toggle logic
        $(document).off('click', '.dependency-group-header').on('click', '.dependency-group-header', function () {
            const $header = $(this);
            const $body = $header.next('.dependency-group-body');
            const $chevron = $header.find('.dep-chevron');

            if ($body.is(':visible')) {
                $body.slideUp(200);
                $chevron.removeClass('bx-chevron-down').addClass('bx-chevron-right');
            } else {
                $body.slideDown(200);
                $chevron.removeClass('bx-chevron-right').addClass('bx-chevron-down');
            }
        });
    };

    function flattenHierarchyForDependencies(data) {
        const items = [];
        const traverse = (item) => {
            if (!item) return;
            items.push(item);
            if (item.subTasks && item.subTasks.length > 0) {
                item.subTasks.forEach(traverse);
            }
        };
        const root = Array.isArray(data) ? data : [data];
        root.forEach(traverse);
        return items;
    }

    function renderDependenciesTab(data) {
        const $container = $('#dependency-tab-content');
        $container.empty();

        const allItems = flattenHierarchyForDependencies(data);
        const sourceItems = allItems.filter(i => i.dependencies && i.dependencies.length > 0);

        if (sourceItems.length === 0) {
            $container.html(`
                <div class="text-center p-5 text-muted">
                    <i class="bx bx-info-circle fs-1 d-block mb-2"></i>
                    No dependencies found for any task in this workstream.
                </div>
            `);
            return;
        }

        sourceItems.forEach(item => {
            const isMeeting = Number(item.typeId) === 2;
            const sortedDeps = [...item.dependencies].sort((a, b) => {
                if (a.direction === 'blocks' && b.direction !== 'blocks') return -1;
                if (a.direction !== 'blocks' && b.direction === 'blocks') return 1;
                return 0;
            });

            const $group = $(`
                <div class="dependency-group mb-3 border rounded shadow-sm">
                    <div class="dependency-group-header p-3 bg-light d-flex justify-content-between align-items-center cursor-pointer">
                        <div class="d-flex align-items-center">
                            <i class="bx bx-chevron-down dep-chevron me-2"></i>
                            <i class="bx bx-${isMeeting ? 'video' : 'task'} me-2 text-primary"></i>
                            <span class="fw-bold text-heading">${item.name || item.title || 'Untitled'}</span>
                            <span class="badge bg-label-secondary ms-2 small">${item.statusName || '-'}</span>
                        </div>
                        <div class="small text-muted">${item.ownerName || ''}</div>
                    </div>
                    <div class="dependency-group-body p-0">
                        <div class="list-group list-group-flush">
                            ${sortedDeps.map(d => {
                const isBlocks = d.direction === 'blocks';
                const arrowIcon = isBlocks ? 'bx-arrow-to-right' : 'bx-arrow-from-right';
                const arrowColor = isBlocks ? 'text-danger' : 'text-info';
                const directionLabel = isBlocks ? 'Blocks' : 'Blocked By';

                return `
                                    <div class="list-group-item d-flex align-items-center py-2 px-4">
                                        <div class="me-4 d-flex align-items-center ${arrowColor}" style="min-width: 120px;">
                                            <i class="bx ${arrowIcon} me-2 fs-5"></i>
                                            <small class="fw-bold text-uppercase" style="font-size: 0.65rem;">${directionLabel}</small>
                                        </div>
                                        <div class="flex-grow-1">
                                            <div class="d-flex align-items-center">
                                                <i class="bx bx-${d.dependencyTypeId == 2 ? 'video' : 'task'} me-2 text-muted small"></i>
                                                <span class="text-heading">${d.dependencyTaskName || '-'}</span>
                                                ${d.dependencyTypeName ? `<span class="ms-2 badge bg-label-dark p-0 px-2" style="font-size: 0.6rem;">${d.dependencyTypeName}</span>` : ''}
                                                ${d.hasCycleRisk ? `<span class="ms-2 badge bg-danger p-0 px-2 animate__animated animate__flash animate__infinite h-100" style="font-size: 0.6rem;">Cycle Risk</span>` : ''}
                                            </div>
                                        </div>
                                    </div>
                                `;
            }).join('')}
                        </div>
                    </div>
                </div>
            `);
            $container.append($group);
        });
    }

    function renderTimelineTab(data) {
        const $container = $('#timeline');
        $container.find('.vertical-timeline').remove();

        const filteredData = applyTimelineFilters(data);
        if (!filteredData || (Array.isArray(filteredData) && filteredData.length === 0)) {
            $container.append('<div class="vertical-timeline p-5 text-center text-muted">No items match your filters.</div>');
            return;
        }

        const $timelineWrapper = $('<div class="vertical-timeline p-2"></div>');
        const items = Array.isArray(filteredData) ? filteredData : [filteredData];

        items.forEach(item => {
            $timelineWrapper.append(renderTimelineItem(item, 0));
        });

        $container.append($timelineWrapper);
    }

    function renderTimelineItem(item, level) {
        const isMeeting = Number(item.typeId) === 2;
        const isCompleted = (item.statusId === 3) || (item.statusName && item.statusName.toLowerCase() === 'completed');
        const typeLabel = isMeeting ? 'Meeting' : 'Task';
        const typeClass = isMeeting ? 'meeting' : 'task';
        const statusClass = isCompleted ? 'completed' : '';
        const hasChildren = item.subTasks && item.subTasks.length > 0;
        const isCollapsed = window.timelineItemStates.get(item.id) === true;

        const formatDate = (dateStr) => {
            if (!dateStr) return '-';
            return moment(dateStr).format('DD MMM YYYY HH:mm');
        };

        let start = item.startDate || item.startDateTime;
        let end = item.endDate || item.endDateTime;
        if (!isMeeting) {
            start = item.runtimeStart || item.startDate;
            end = item.runtimeEnd || item.endDate;
        }

        const indent = level * 30;

        const $item = $(`
            <div class="timeline-item ${typeClass} ${statusClass} ${hasChildren ? 'has-children' : ''} ${isCollapsed ? 'collapsed' : ''}" 
                 style="margin-left: ${indent}px;" data-task-id="${item.id}">
                <div class="timeline-dot"></div>
                <div class="timeline-content">
                    <div class="card shadow-none">
                        <div class="card-body p-3">
                            <div class="d-flex justify-content-between align-items-start mb-2">
                                <div>
                                    <h6 class="mb-1 fw-bold text-heading">
                                        ${hasChildren ? '<i class="bx bx-chevron-down timeline-chevron"></i>' : ''}
                                        ${item.name || item.title || 'Untitled'}
                                        ${hasChildren ? `<span class="child-count-badge text-primary ms-1">(${item.subTasks.length})</span>` : ''}
                                    </h6>
                                    <div class="d-flex flex-wrap gap-2 align-items-center">
                                        <span class="badge bg-label-${isMeeting ? 'primary' : 'secondary'} small p-1 px-2">
                                            <i class="bx bx-${isMeeting ? 'video' : 'task'} me-1"></i>${typeLabel}
                                        </span>
                                        ${item.statusName ? `
                                            <span class="badge bg-label-${isCompleted ? 'success' : 'info'} small p-1 px-2">
                                                ${item.statusName}
                                            </span>
                                        ` : ''}
                                    </div>
                                </div>
                                <div class="text-end">
                                    <small class="text-muted d-block"><i class="bx bx-user me-1"></i>${item.ownerName || 'No Owner'}</small>
                                </div>
                            </div>
                            
                            <div class="d-flex flex-wrap gap-3 small text-muted">
                                <span><i class="bx bx-time-five me-1"></i>${formatDate(start)} – ${formatDate(end)}</span>
                                ${isMeeting ? `
                                    <span class="badge bg-label-${item.isVirtual ? 'info' : 'warning'} p-0 px-2">
                                        ${item.isVirtual ? 'Virtual' : 'Physical'}
                                    </span>
                                ` : ''}
                            </div>

                            ${isMeeting && item.agendaItems && item.agendaItems.length > 0 ? `
                                <div class="timeline-agenda mt-3 p-3">
                                    <small class="text-muted d-block mb-2 fw-bold"><i class="bx bx-list-ul me-1"></i>Agenda:</small>
                                    ${item.agendaItems.map(a => `
                                        <div class="d-flex justify-content-between align-items-center mb-1 border-bottom border-light pb-1 last-child-no-border">
                                            <span class="small">• ${a.title}</span>
                                            <span class="small text-muted font-monospace">${moment(a.startDateTime).format('HH:mm')} - ${moment(a.endDateTime).format('HH:mm')}</span>
                                        </div>
                                    `).join('')}
                                </div>
                            ` : ''}
                        </div>
                    </div>
                </div>
            </div>
        `);

        if (hasChildren) {
            const $childrenWrap = $(`<div class="timeline-children" style="${isCollapsed ? 'display: none;' : ''}"></div>`);
            item.subTasks.forEach(child => {
                $childrenWrap.append(renderTimelineItem(child, level + 1));
            });
            return $item.add($childrenWrap);
        }

        return $item;
    }

    function getParentTaskIdFromUrl() {
        const segments = window.location.pathname.split('/').filter(Boolean);
        const ppmIndex = segments.indexOf('ppm');
        if (ppmIndex > -1 && segments.length > ppmIndex + 1) {
            return segments[ppmIndex + 1];
        }
        return null;
    }

    function getWorkflowIdFromUrl() {
        return new URLSearchParams(window.location.search).get('workflowId');
    }

    function getProjectNameFromUrl() {
        return new URLSearchParams(window.location.search).get('projectName');
    }

    function getWorkstreamNameFromUrl() {
        return new URLSearchParams(window.location.search).get('workstreamName');
    }

    // ============================================
    // SAVE MEETING LOGIC
    // ============================================
    window.meetingHandleSave = async function () {
        const form = document.getElementById('create-meeting-form');
        if (!form) return;

        let hasError = false;

        // --- 1. DETAILS VALIDATION ---


        // Title
        const $title = $('#meeting-title');
        const title = $title.val() ? $title.val().trim() : '';
        if (!title) {
            $title.addClass('is-invalid');
            hasError = true;
        } else {
            $title.removeClass('is-invalid');
        }

        // Attendees
        const $attendees = $('#meeting-attendees');
        const attendees = $attendees.val();
        if (!attendees || attendees.length === 0) {
            $attendees.addClass('is-invalid');
            hasError = true;
        } else {
            $attendees.removeClass('is-invalid');
        }

        // Start Date
        const $startD = $('#meeting-start-date');
        const startD = $startD.val();
        if (!startD) {
            $startD.addClass('is-invalid');
            hasError = true;
        } else {
            $startD.removeClass('is-invalid');
        }

        // Start Time
        const $startT = $('#meeting-start-time');
        const startT = $startT.val();
        if (!startT) {
            $startT.addClass('is-invalid');
            hasError = true;
        } else {
            $startT.removeClass('is-invalid');
        }

        // End Date
        const $endD = $('#meeting-end-date');
        const endD = $endD.val();
        if (!endD) {
            $endD.addClass('is-invalid');
            hasError = true;
        } else {
            $endD.removeClass('is-invalid');
        }

        // End Time
        const $endT = $('#meeting-end-time');
        const endT = $endT.val();
        if (!endT) {
            $endT.addClass('is-invalid');
            hasError = true;
        } else {
            $endT.removeClass('is-invalid');
        }

        // Owner
        const $owner = $('#meeting-owner');
        const owner = $owner.val();
        if (!owner) {
            $owner.addClass('is-invalid');
            hasError = true;
        } else {
            $owner.removeClass('is-invalid');
        }

        // Virtual / Location
        const isVirtual = document.getElementById('meeting-is-virtual').checked;
        let location = null;
        let meetingLink = null;

        const $link = $('#meeting-link');
        const $loc = $('#meeting-location');

        if (isVirtual) {
            meetingLink = $link.val() ? $link.val().trim() : '';
            if (!meetingLink) {
                $link.addClass('is-invalid');
                hasError = true;
            } else {
                $link.removeClass('is-invalid');
            }
            // Clear other
            $loc.val('').removeClass('is-invalid');
        } else {
            location = $loc.val() ? $loc.val().trim() : '';
            if (!location) {
                $loc.addClass('is-invalid');
                hasError = true;
            } else {
                $loc.removeClass('is-invalid');
            }
            // Clear other
            $link.val('').removeClass('is-invalid');
        }

        // --- 2. AGENDA VALIDATION ---
        const agendaItems = [];
        const $rows = $('#agenda-items-container .agenda-row');

        $rows.each(function (index) {
            const $row = $(this);
            const $aTitle = $row.find('.agenda-title');
            const $aAttendees = $row.find('.agenda-attendees');
            const $aStartD = $row.find('.agenda-start-date');
            const $aStartT = $row.find('.agenda-start-time');
            const $aEndD = $row.find('.agenda-end-date');
            const $aEndT = $row.find('.agenda-end-time');

            const aTitle = $aTitle.val() ? $aTitle.val().trim() : '';
            const aAttendees = $aAttendees.val();
            const aStartD = $aStartD.val();
            const aStartT = $aStartT.val();
            const aEndD = $aEndD.val();
            const aEndT = $aEndT.val();

            let rowHasError = false;

            if (!aTitle) { $aTitle.addClass('is-invalid'); rowHasError = true; } else { $aTitle.removeClass('is-invalid'); }
            if (!aAttendees || aAttendees.length === 0) { $aAttendees.addClass('is-invalid'); rowHasError = true; } else { $aAttendees.removeClass('is-invalid'); }
            if (!aStartD) { $aStartD.addClass('is-invalid'); rowHasError = true; } else { $aStartD.removeClass('is-invalid'); }
            if (!aStartT) { $aStartT.addClass('is-invalid'); rowHasError = true; } else { $aStartT.removeClass('is-invalid'); }
            if (!aEndD) { $aEndD.addClass('is-invalid'); rowHasError = true; } else { $aEndD.removeClass('is-invalid'); }
            if (!aEndT) { $aEndT.addClass('is-invalid'); rowHasError = true; } else { $aEndT.removeClass('is-invalid'); }

            if (rowHasError) {
                hasError = true;
            } else {
                agendaItems.push({
                    order: index + 1,
                    title: aTitle,
                    startDateTime: `${aStartD} ${aStartT}`,
                    endDateTime: `${aEndD} ${aEndT}`,
                    attendees: aAttendees
                });
            }
        });

        if (hasError) return; // Standard visual validation implies we stop here

        // --- 3. TIME ALIGNMENT CHECK (Kept as Toast) ---
        if (agendaItems.length > 0) {
            const meetStart = `${startD} ${startT}`;
            const meetEnd = `${endD} ${endT}`;
            const firstStart = agendaItems[0].startDateTime;
            const lastEnd = agendaItems[agendaItems.length - 1].endDateTime;

            // Simple string comparison works for ISO format YYYY-MM-DD HH:mm
            if (firstStart !== meetStart) {
                return showToast("First agenda item start time must match meeting start time.", "error");
            }
            if (lastEnd !== meetEnd) {
                return showToast("Last agenda item end time must match meeting end time.", "error");
            }
        }

        // --- 4. CONSTRUCT PAYLOAD ---
        const classificationId = Number($('#meeting-classification').val());
        const notesHtml = window.meetingNotesQuill ? window.meetingNotesQuill.root.innerHTML : "";
        const meetingId = window.editingMeetingId || null;

        const payload = {
            id: meetingId,
            parentTaskId: getParentTaskIdFromUrl(),
            title: title,
            classificationId: classificationId || 0,
            ownerId: owner,
            isVirtual: isVirtual,
            location: location,
            meetingLink: meetingLink,
            startDateTime: `${startD} ${startT}`,
            endDateTime: `${endD} ${endT}`,
            attendees: attendees,
            agendaItems: agendaItems,
            notesHtml: notesHtml,
            createdBy: window.getUserName()
        };

        try {
            const response = await fetch(`${API.ppm}/Task/SaveMeeting`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            const result = await response.json();

            if (response.ok && (!result.errors || result.errors.length === 0)) {
                const msg = payload.id ? "Meeting updated successfully." : "Meeting created successfully.";
                showToast(msg, "success");

                // Trigger cancel to reset and close
                $('[data-action="cancel-create"]').trigger('click');

                // Refresh DataTable
                if (typeof dtWorkstreamTask !== 'undefined' && dtWorkstreamTask !== null) {
                    const parentTaskId = getParentTaskIdFromUrl();
                    const workflowId = getWorkflowIdFromUrl();
                    const projectName = getProjectNameFromUrl();
                    const workstreamName = getWorkstreamNameFromUrl();

                    const newUrl =
                        `${API.ppm}/Workstream/GetTaskHierarchyByParentTask/${parentTaskId}` +
                        `?workflowId=${workflowId || ''}` +
                        `&projectName=${encodeURIComponent(projectName || '')}` +
                        `&workstreamName=${encodeURIComponent(workstreamName || '')}`;

                    dtWorkstreamTask.ajax.url(newUrl).load(null, false);
                } else if (typeof initWorkstreamTaskTable === 'function') {
                    initWorkstreamTaskTable();
                }
            } else {
                const errorMsg = (result.errors && result.errors.length > 0) ? result.errors[0] : (result.message || "Failed to save meeting.");
                showToast(errorMsg, "error");
            }
        } catch (error) {
            console.error("SaveMeeting Error:", error);
            showToast('Unexpected error occurred while saving meeting.', 'error');
        }
    };

    // Save Handler
    $(document).off('click', '[data-action="save-meeting"]').on('click', '[data-action="save-meeting"]', function () {
        if (typeof window.meetingHandleSave === 'function') {
            window.meetingHandleSave();
        }
    });

    // ============================================
    // RESET FORM LOGIC
    // ============================================
    window.meetingResetCreateForm = function () {
        // 1. FORM RESET
        const form = document.getElementById('create-meeting-form');
        if (form) {
            form.reset();
            delete form.dataset.meetingInitialized;
            delete form.dataset.initialAgendaCreated;
        }

        // 2. SELECT2 RESET
        $('#meeting-classification').val(null).trigger('change');
        $('#meeting-attendees').val(null).trigger('change');
        $('#meeting-owner').val(null).trigger('change');
        $('#meeting-start-time').val(null).trigger('change');
        $('#meeting-end-time').val(null).trigger('change');

        // 3. FLATPICKR RESET
        if (window.startDatePicker) {
            window.startDatePicker.clear();
            window.startDatePicker.setDate(new Date(), true);
        }
        if (window.endDatePicker) {
            window.endDatePicker.clear();
        }

        // 4. QUILL RESET (Meeting Notes)
        if (window.meetingNotesQuill) {
            window.meetingNotesQuill.setText('');
        }

        // 5. AGENDA RESET
        const agendaContainer = document.getElementById('agenda-items-container');
        if (agendaContainer) {
            agendaContainer.innerHTML = `
                <div class="agenda-empty-state alert alert-primary alert-dismissible d-flex align-items-start gap-2">
                    <i class="bx bx-calendar fs-4 mt-1"></i>
                    <div>
                        <h5 class="alert-heading d-flex align-items-center flex-wrap gap-1 mb-1">No agenda items yet</h5>
                        <p class="mb-0">
                            Use <b>Add Item</b> to define the meeting flow and timings.
                        </p>
                    </div>
                </div>
            `;
        }

        // 6. VIRTUAL TOGGLE RESET
        const virtualToggle = document.getElementById('meeting-is-virtual');
        if (virtualToggle) {
            virtualToggle.checked = false;
        }

        const linkDiv = document.getElementById('container-meeting-link');
        const locDiv = document.getElementById('container-meeting-location');
        if (linkDiv) linkDiv.classList.add('d-none');
        if (locDiv) locDiv.classList.remove('d-none');

        // 7. GLOBAL STATE CLEAR
        delete window.meetingUsersCache;
    };

    // Global Cancel Handler
    $(document).off('click', '[data-action="cancel-create"]').on('click', '[data-action="cancel-create"]', function () {
        if (typeof window.meetingResetCreateForm === 'function') {
            window.meetingResetCreateForm();
        }
    });

    // Auto-trigger when form appears in DOM using MutationObserver
    const observer = new MutationObserver((mutations) => {
        for (const mutation of mutations) {
            if (mutation.addedNodes.length) {
                const form = document.getElementById('create-meeting-form');
                if (form && !form.dataset.meetingInitialized) {
                    window.initCreateWorkstreamMeeting();
                }
            }
        }
    });

    // Start observing body for additions
    observer.observe(document.body, { childList: true, subtree: true });

    // === DELETE CONFIRM MODAL START ===
    let recordToDelete = null;
    let rowToDelete = null;

    $(document).on('click', '.delete-record', function (e) {
        e.preventDefault();
        const tr = $(this).closest('tr');
        if (!dtWorkstreamTask) return;

        const row = dtWorkstreamTask.row(tr);
        const data = row.data();
        if (!data) return;

        recordToDelete = data;
        rowToDelete = row;

        const $modal = $('#deleteConfirmModal');
        const $modalBody = $modal.find('.modal-body');

        let bodyHtml = `
            <div class="text-center mb-4">
                <i class="bx bx-error-circle text-danger fs-1 mb-2"></i>
                <h5 class="mb-1">Are you sure you want to delete this ${Number(data.typeId) === 2 ? 'meeting' : 'task'}?</h5>
                <p class="text-muted">This action cannot be undone.</p>
            </div>
            <div class="alert alert-light border border-dashed rounded p-3 mb-0">
                <div class="d-flex align-items-center mb-2">
                    <i class="bx bx-${Number(data.typeId) === 2 ? 'video' : 'task'} text-primary me-2"></i>
                    <span class="fw-bold text-heading">${data.name || data.title || 'Untitled'}</span>
                </div>
        `;

        // Check for subtasks
        if (data.subTasks && data.subTasks.length > 0) {
            bodyHtml += `
                <div class="mt-3 pt-3 border-top">
                    <div class="d-flex align-items-center text-danger mb-2">
                        <i class="bx bx-error me-2"></i>
                        <small class="fw-bold text-uppercase" style="letter-spacing: 0.5px;">Warning: Sub-tasks detected</small>
                    </div>
                    <p class="small text-muted mb-2">The following sub-tasks will also be deleted:</p>
                    <ul class="list-unstyled mb-0 small ps-2">
                        ${data.subTasks.map(st => `
                            <li class="mb-1 d-flex align-items-center">
                                <i class="bx bx-subdirectory-right text-muted me-2"></i>
                                <span>${st.name || st.title || 'Untitled Sub-task'}</span>
                            </li>
                        `).join('')}
                    </ul>
                </div>
            `;
        }

        bodyHtml += `</div>`;
        $modalBody.html(bodyHtml);

        const modalInstance = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
        modalInstance.show();
    });

    $(document).off('click', '#confirmDeleteBtn').on('click', '#confirmDeleteBtn', async function () {
        const $btn = $(this);
        const originalText = $btn.html();

        // 1. Disable and show loading state
        $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-2" role="status"></span>Deleting...');

        try {
            const userId = typeof currentUserId !== 'undefined' ? currentUserId : window.currentUserId;

            // 2. Real API Call
            const response = await fetch(`${API.ppm}/Task/DeleteWorkstreamTask/${recordToDelete.id}?modifiedBy=${userName}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    
                }
            });

            if (!response.ok) throw new Error("Delete request failed");

            // 3. Success UI
            showToast("Task deleted successfully", "success");

            // 4. Refresh DataTable without breaking paging
            if (dtWorkstreamTask) {
                const parentTaskId = getParentTaskIdFromUrl();
                const workflowId = getWorkflowIdFromUrl();
                const projectName = getProjectNameFromUrl();
                const workstreamName = getWorkstreamNameFromUrl();

                const newUrl =
                    `${API.ppm}/Workstream/GetTaskHierarchyByParentTask/${parentTaskId}` +
                    `?workflowId=${workflowId}` +
                    `&projectName=${encodeURIComponent(projectName)}` +
                    `&workstreamName=${encodeURIComponent(workstreamName)}`;

                dtWorkstreamTask.ajax.url(newUrl).load(null, false);
            } else {
                initWorkstreamTaskTable();
            }

            // 5. Close Modal
            const modalEl = document.getElementById('deleteConfirmModal');
            const modalInstance = bootstrap.Modal.getInstance(modalEl);
            if (modalInstance) modalInstance.hide();

            // 6. Cleanup & Reset State
            $btn.prop('disabled', false).html(originalText);
            recordToDelete = null;
            rowToDelete = null;

            $(modalEl).one('hidden.bs.modal', function () {
                $(this).find('.modal-body').html('');
            });

        } catch (err) {
            console.error("Delete error:", err);
            showToast("Error deleting task. Please try again.", "danger");

            // Re-enable button on error
            $btn.prop('disabled', false).html(originalText);
        }
    });
    // === DELETE CONFIRM MODAL END ===

})();

document.addEventListener("DOMContentLoaded", async function () {
    renderBreadcrumb();
    initWorkstreamTaskTable();
    initCreateViewToggles();
    initTimelineTab(); // Initialized Timeline listener
    initDependenciesTab(); // Initialized Dependencies listener
    loadTaskCategories();
    loadTaskPriority();
    loadTaskStatus(false);
    loadDependencyTypes();
    loadDependencyDirections();
    loadDependencyReasons();
    loadDependencyTasks("");
});
