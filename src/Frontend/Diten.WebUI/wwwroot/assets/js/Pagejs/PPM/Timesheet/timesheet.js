/**
 * Enterprise PPM Timesheet Component
 * Logic for Employee Execution View and Manager Oversight View
 */

'use strict';
let timeEntryToDelete = {
    timeTrackerId: null
};

function getUserId() {
    const token = localStorage.getItem("token");
    if (!token) return null;

    const decoded = decodeJWT(token);
    return decoded
        ? decoded["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"]
        : null;
}
function decodeJWT(token) {

    const base64Url = token.split('.')[1];  // Token'ın ikinci kısmı payload'dır
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');  // Base64 formatını düzelt
    const jsonPayload = decodeURIComponent(atob(base64).split('').map(function (c) {
        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
    }).join(''));
    return JSON.parse(jsonPayload);
}

window.currentUserId = getUserId();
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

function initQuickTimerTable() {

    const tableSelector = '.quick-timer-table';
    const tableEl = document.querySelector(tableSelector);
    if (!tableEl) return;

    // tekrar init edilmesin
    if (window.dtQuickTimer) {
        window.dtQuickTimer.destroy();
        window.dtQuickTimer = null;
    }

    window.dtQuickTimer = new DataTable(tableEl, {
        destroy: true,
        stateSave: false,
        serverSide: true,
        processing: true,
        pageLength: 100,
        lengthMenu: [10, 25, 50, 100],

        ajax: (data, callback) => {
            const employeeId = $('#employeeContextSelector').val() || window.currentUserId;
            const dateFilter =
                document.querySelector('[data-date-filter].active')?.dataset.dateFilter || 'today';

            const payload = {
                employeeId,
                dateFilter,
                draw: data.draw,
                start: data.start,
                length: data.length,
                search: {
                    value: data.search?.value || ''
                }
            };

            fetch(`${API.ppm}/TimeSheet/GetQuickTimerTasks`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
                .then(r => r.json())
                .then(res => {
                    const d = res?.data || {};
                    callback({
                        draw: data.draw,
                        recordsTotal: d.recordsTotal ?? 0,
                        recordsFiltered: d.recordsFiltered ?? 0,
                        data: d.data || []
                    });
                })
                .catch(err => {
                    console.error('❌ QuickTimer error', err);
                    callback({
                        draw: data.draw,
                        recordsTotal: 0,
                        recordsFiltered: 0,
                        data: []
                    });
                });
        },

        columns: [
            { data: null },               // responsive control
            { data: 'taskName' },         // Task Details
            { data: 'statusName' },       // Status
            { data: 'priorityName' },     // Priority
            { data: null },               // Hours
            { data: 'taskId' }            // Action
        ],

        columnDefs: [
            // Responsive control
            {
                targets: 0,
                className: 'control',
                orderable: false,
                searchable: false,
                defaultContent: '',
                render: () => ''
            },

            // Task Details
            {
                targets: 1,
                responsivePriority: 1,
                render: (data, type, row) => {
                    if (type === 'filter' || type === 'sort') {
                        return `${row.taskName} ${row.description || ''}`;
                    }
                    const shortDescription = htmlToPlainText(row.description);

                    return `
                        <div>
                            <div class="fw-medium text-heading">
                                ${row.taskName}
                            </div>
                            <small class="text-heading truncate-2" title="${shortDescription}">
                        ${shortDescription}
                    </small>
                        </div>
                    `;
                }
            },

            // Status
            {
                targets: 2,
                responsivePriority: 2,
                render: (data, type) => {
                    if (type === 'filter' || type === 'sort') return data || '';
                    const map = {
                        'To do': 'warning',
                        'In Progress': 'info',
                        'Completed': 'success',
                        'Cancelled': 'danger'
                    };
                    return `<span class="badge bg-${map[data] || 'secondary'} text-nowrap">${data}</span>`;
                }
            },

            // Priority
            {
                targets: 3,
                responsivePriority: 2,
                render: (data, type) => {
                    if (type === 'filter' || type === 'sort') return data || '';
                    const map = {
                        'Low': 'primary',
                        'Medium': 'info',
                        'High': 'warning',
                        'Critical': 'danger'
                    };
                    return `<span class="badge bg-label-${map[data] || 'secondary'} text-nowrap">${data}</span>`;
                }
            },

            // Hours
            {
                targets: 4,
                responsivePriority: 1,
                render: (data, type, row) => {
                    const spentH = Math.round(((row.spentMinutes || 0) / 60) * 10) / 10;
                    const estimatedH = Math.round(((row.estimatedHour || 0) / 60) * 10) / 10;

                    // Search & sort için plain text
                    if (type === 'filter' || type === 'sort') {
                        return `${spentH} ${estimatedH}`;
                    }

                    return `
            <span class="fw-medium">${spentH}h</span>
            <span class="text-muted">/ ${estimatedH}h</span>
        `;
                }
            },

            // Action
            {
                targets: 5,
                responsivePriority: 0,
                orderable: false,
                searchable: false,
                className: 'dt-action-col text-center',
                render: (data, type, row) => {

                    const selectedEmployeeId = $('#employeeContextSelector').val();
                    const currentUserId = window.currentUserId;

                    // 🔒 Başkasının task'ıysa action YOK
                    if (!selectedEmployeeId || selectedEmployeeId !== currentUserId || row.statusId === 3) {
                        return ''; // boş hücre
                    }

                    const isRunning = row.taskId === window.timesheetApp?.runningTimerId;

                    return `
        <button class="btn btn-sm ${isRunning ? 'btn-outline-danger' : 'btn-outline-success'} btn-start-task"
                data-task-id="${row.taskId}" data-task-name="${row.taskName}">
            <i class="icon-base bx ${isRunning ? 'bx-stop' : 'bx-play'}"></i>
            ${isRunning ? 'Stop' : 'Start'}
        </button>
    `;
                }
            }
        ],

        order: [],

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

        responsive: {
            details: {
                display: DataTable.Responsive.display.modal({
                    header: row => 'Task Details'
                }),
                type: 'column'
            }
        },

        initComplete: function () {
            fixDataTableLayout();
        },

        drawCallback: function () {
            fixDataTableLayout();
        }
    });
}

function initTimeEntriesTable() {
    const tableEl = document.querySelector('.time-entry-table');
    if (!tableEl) return;

    if (window.dtTimeEntries) {
        window.dtTimeEntries.destroy();
        window.dtTimeEntries = null;
    }

    const formatDate = (isoStr) => {
        if (!isoStr) return '';
        const d = new Date(isoStr);
        const pad = (n) => (n < 10 ? '0' + n : n);
        return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    };

    window.dtTimeEntries = new DataTable(tableEl, {
        destroy: true,
        stateSave: false,
        serverSide: true,
        processing: true,
        pageLength: 100,
        lengthMenu: [10, 25, 50, 100],
        ajax: (data, callback) => {
            const employeeId = $('#employeeContextSelector').val() || window.currentUserId;
            const dateFilter = document.querySelector('[data-date-filter].active')?.dataset.dateFilter || 'today';

            const payload = {
                draw: data.draw,
                start: data.start,
                length: data.length,
                search: {
                    value: data.search?.value || ''
                },
                employeeId,
                dateFilter
            };

            fetch(`${API.ppm}/TimeSheet/GetTimeEntries`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
                .then(r => r.json())
                .then(res => {
                    const d = res?.data || {};
                    callback({
                        draw: d.draw,
                        recordsTotal: d.recordsTotal || 0,
                        recordsFiltered: d.recordsFiltered || 0,
                        data: d.data || []
                    });
                })
                .catch(err => {
                    console.error('❌ TimeEntries error', err);
                    callback({
                        draw: data.draw,
                        recordsTotal: 0,
                        recordsFiltered: 0,
                        data: []
                    });
                });
        },
        columns: [
            { data: null },
            { data: 'start' },
            { data: 'taskName' },
            { data: 'durationFormatted' },
            { data: 'timeTrackerId' }
        ],
        columnDefs: [
            {
                targets: 0,
                className: 'control',
                orderable: false,
                defaultContent: '',
                render: () => ''
            },
            {
                targets: 1,
                render: (data) => formatDate(data)
            },
            {
                targets: 2,
                render: (data, type, row) => {
                    const wf = (!row.description || row.description === '-') ? '' : row.description;
                    if (type === 'filter' || type === 'sort') {
                        return `${row.taskName} ${row.description || ''}`;
                    }
                    const shortDescription = htmlToPlainText(row.description);



                    return `
                        <div class="d-flex flex-column">
                            <span class="fw-bold text-heading">${row.taskName}</span>
                            <small class="text-heading truncate-2" title="${shortDescription}">
                        ${shortDescription}
                    </small>
                        </div>`;
                }
            },
            {
                targets: 3,
                render: (data) => data
            },
            {
                targets: 4,
                responsivePriority: 0,
                className: 'dt-action-col',
                orderable: false,
                searchable: false,
                render: (data, type, row) => {

                    const selectedEmployeeId = $('#employeeContextSelector').val();
                    const currentUserId = window.currentUserId;

                    // 🔒 Başkasının kaydıysa → ACTION YOK
                    if (!selectedEmployeeId || selectedEmployeeId !== currentUserId) {
                        return '';
                    }

                    const isCompleted = row.statusId === 3;
                    const hasWorkflowTask = row.taskId && row.taskId.trim() !== '';
                    return `
<div class="dropdown">
    <button class="btn btn-icon" data-bs-toggle="dropdown">
        <i class="bx bx-dots-vertical-rounded"></i>
    </button>

    <div class="dropdown-menu dropdown-menu-end">

        <!-- Overview (SOON) -->
        <div class="position-relative">
            <a class="dropdown-item d-flex align-items-center disabled"
               href="javascript:;">
                <i class="icon-sm bx bx-show me-2"></i>
                Overview
            </a>
            <span class="badge bg-secondary position-absolute top-50 end-0 translate-middle-y me-3">
                Soon
            </span>
        </div>

        <!-- Edit (SOON) -->
        <div class="position-relative">
            <a class="dropdown-item d-flex align-items-center disabled"
               href="javascript:;">
                <i class="icon-sm bx bx-edit-alt me-2"></i>
                Edit
            </a>
            <span class="badge bg-secondary position-absolute top-50 end-0 translate-middle-y me-3">
                Soon
            </span>
        </div>

        <div class="dropdown-divider"></div>

${(!isCompleted && hasWorkflowTask) ? `
    <!-- Mark as Completed (SOON) -->
    <div class="position-relative">
        <a class="dropdown-item d-flex align-items-center disabled"
           href="javascript:;">
            <i class="icon-sm bx bx-check-circle me-2"></i>
            Mark as Completed
        </a>
        <span class="badge bg-secondary position-absolute top-50 end-0 translate-middle-y me-3">
            Soon
        </span>
    </div>
` : ''}

${!isCompleted ? `
    <!-- Delete (ACTIVE) -->
    <a class="dropdown-item d-flex align-items-center text-danger delete-record"
       href="javascript:;"
       data-id="${row.timeTrackerId}">
        <i class="icon-sm bx bx-trash me-2"></i>
        Delete
    </a>
` : ''}
    </div>
</div>
`;
                }
            }

        ],
        layout: {
            topStart: {
                rowClass: 'row mx-3 my-0 justify-content-between',
                features: [{ pageLength: { menu: [10, 25, 50, 100], text: '_MENU_' } }]
            },
            topEnd: {
                features: [{ search: { placeholder: 'Search', text: '_INPUT_' } }]
            },
            bottomStart: {
                rowClass: 'row mx-3 justify-content-between',
                features: ['info']
            },
            bottomEnd: {
                paging: { firstLast: false }
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
        responsive: {
            details: {
                display: DataTable.Responsive.display.modal({
                    header: row => 'Entry Details'
                }),
                type: 'column'
            }
        },
        initComplete: function () {
            if (typeof fixDataTableLayout === 'function') fixDataTableLayout();
        },
        drawCallback: function () {
            if (typeof fixDataTableLayout === 'function') fixDataTableLayout();
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
function getActiveDateFilter() {
    const btn = document.querySelector('.nav-link[data-date-filter].active');
    return btn ? btn.dataset.dateFilter : 'today';
}

function calculateTrend(current, previous) {
    if (previous === 0) {
        return {
            percent: 100,
            direction: 'up',
            css: 'text-success'
        };
    }

    const diff = current - previous;
    const percent = Math.abs((diff / previous) * 100);

    return {
        percent: percent.toFixed(1),
        direction: diff >= 0 ? 'up' : 'down',
        css: diff >= 0 ? 'text-success' : 'text-danger'
    };
}
function getPreviousDateFilter(current) {
    switch (current) {
        case 'today': return 'yesterday';
        case 'week': return 'lastweek';
        case 'month': return 'lastmonth';
        default: return null;
    }
}
function renderTrend(el, trend) {
    if (!el) return;

    const icon = trend.direction === 'up'
        ? 'bx bx-up-arrow-alt'
        : 'bx bx-down-arrow-alt';

    el.innerHTML = `
        <i class="icon-base bx ${icon}"></i>
        ${trend.percent}%
    `;
    el.className = `trend-text text-nowrap fw-medium ${trend.css}`;
}

function minutesToHours(minutes) {
    return Math.round((minutes / 60) * 10) / 10;
}

function getCapacityMinutesByDateFilter(dateFilter) {
    const DAILY_MINUTES = 480;
    const now = new Date();

    switch (dateFilter) {
        case 'today':
            return DAILY_MINUTES;

        case 'week':
            // 5 workdays
            return 5 * DAILY_MINUTES;

        case 'month': {
            const year = now.getFullYear();
            const month = now.getMonth();
            let workDays = 0;

            const daysInMonth = new Date(year, month + 1, 0).getDate();

            for (let d = 1; d <= daysInMonth; d++) {
                const day = new Date(year, month, d).getDay();
                // 1-5 → Mon-Fri
                if (day >= 1 && day <= 5) {
                    workDays++;
                }
            }

            return workDays * DAILY_MINUTES;
        }

        case 'all': {
            // 🔥 FIX: From 01.01.2026 to today (workdays only)
            const startDate = new Date(2026, 0, 1); // 01 Jan 2026
            const endDate = new Date(
                now.getFullYear(),
                now.getMonth(),
                now.getDate()
            );

            let workDays = 0;
            const cursor = new Date(startDate);

            while (cursor <= endDate) {
                const day = cursor.getDay();
                if (day >= 1 && day <= 5) {
                    workDays++;
                }
                cursor.setDate(cursor.getDate() + 1);
            }

            return workDays * DAILY_MINUTES;
        }

        default:
            return DAILY_MINUTES;
    }
}

function getRemainingHoursTooltipHtml(dateFilter) {
    switch (dateFilter) {
        case 'today':
            return `
                <div class="text-start">
                    <div class="fw-semibold mb-1">Remaining Hours</div>
                    <div class="small text-muted">
                        <i class="bx bx-time-five me-1"></i>
                        1 working day × 8h
                    </div>
                    <div class="small text-muted">
                        − Logged time today
                    </div>
                </div>
            `;

        case 'week':
            return `
                <div class="text-start">
                    <div class="fw-semibold mb-1">Remaining Hours</div>
                    <div class="small text-muted">
                        <i class="bx bx-calendar-week me-1"></i>
                        5 working days × 8h
                    </div>
                    <div class="small text-muted">
                        − Logged time this week
                    </div>
                </div>
            `;

        case 'month':
            return `
                <div class="text-start">
                    <div class="fw-semibold mb-1">Remaining Hours</div>
                    <div class="small text-muted">
                        <i class="bx bx-calendar me-1"></i>
                        Working days in this month × 8h
                    </div>
                    <div class="small text-muted">
                        − Logged time this month
                    </div>
                </div>
            `;

        case 'all':
            return `
                <div class="text-start">
                    <div class="fw-semibold mb-1">Remaining Hours</div>
                    <div class="small text-muted">
                        <i class="bx bx-history me-1"></i>
                        From 01.01.2026 until today
                    </div>
                    <div class="small text-muted">
                        Working days × 8h (weekends excluded)
                    </div>
                    <div class="small text-muted">
                        − Total logged time
                    </div>
                </div>
            `;

        default:
            return '';
    }
}

function loadTimesheetKpis() {
    const employeeId = $('#employeeContextSelector').val() || window.currentUserId;
    const dateFilter = getActiveDateFilter();

    const payload = {
        employeeId,
        dateFilter,
        draw: 1,
        start: 0,
        length: 1000,
        search: { value: '' }
    };

    const pEntries = fetch(`${API.ppm}/TimeSheet/GetTimeEntries`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    }).then(r => r.json());

    const pTasks = fetch(`${API.ppm}/TimeSheet/GetQuickTimerTasks`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    }).then(r => r.json());

    Promise.all([pEntries, pTasks])
        .then(([entriesRes, tasksRes]) => {

            const entries = entriesRes?.data?.data || [];
            const tasks = tasksRes?.data?.data || [];

            // =================================================
            // 1️⃣ ACTIVE TASK COUNT
            // =================================================
            let activeTaskCount = 0;

            tasks.forEach(t => {
                const isCompleted = t.statusId === 3;
                const hasActivity =
                    t.isRunning === true ||
                    (t.spentMinutes && t.spentMinutes > 0);

                if (!isCompleted && hasActivity) {
                    activeTaskCount++;
                }
            });

            // =================================================
            // 2️⃣ TOTAL SPENT (MINUTES)
            // =================================================
            let totalSpentMinutes = 0;
            entries.forEach(e => {
                totalSpentMinutes += Number(e.durationInMinutes || 0);
            });

            // =================================================
            // 3️⃣ REMAINING (MINUTES) - DYNAMIC CAPACITY
            // =================================================
            const capacityMinutes = getCapacityMinutesByDateFilter(dateFilter);

            const remainingMinutes = Math.max(
                capacityMinutes - totalSpentMinutes,
                0
            );

            // TEST CASES:
            // TODAY
            // spent: 240 → remaining: 240 → 4h

            // WEEK
            // capacity: 2400 (5×480)
            // spent: 1200 → remaining: 1200 → 20h

            // MONTH (21 workdays)
            // capacity: 10080
            // spent: 3000 → remaining: 7080 → 118h

            // OVER
            // spent > capacity → remaining = 0

            // =================================================
            // 4️⃣ UI CONVERSION & UPDATE
            // =================================================
            const remainingHours = minutesToHours(remainingMinutes);

            const activeTasksEl = document.getElementById('activeTasksValue');
            const remainingHoursEl = document.getElementById('remainingHoursValue');

            if (activeTasksEl) {
                activeTasksEl.textContent = activeTaskCount;
            }

            if (remainingHoursEl) {
                remainingHoursEl.textContent = `${remainingHours}h`;

                // Önce varsa eski tooltip'i dispose et
                if (remainingHoursEl._tooltipInstance) {
                    remainingHoursEl._tooltipInstance.dispose();
                }

                remainingHoursEl.setAttribute('data-bs-toggle', 'tooltip');
                remainingHoursEl.setAttribute('data-bs-placement', 'top');
                remainingHoursEl.setAttribute('data-bs-html', 'true');
                remainingHoursEl.setAttribute(
                    'data-bs-title',
                    getRemainingHoursTooltipHtml(dateFilter)
                );

                // Yeni tooltip oluştur
                remainingHoursEl._tooltipInstance = new bootstrap.Tooltip(remainingHoursEl);
            }

            // =================================================
            // 5️⃣ TREND (Dummy / Placeholder)
            // =================================================
            const prevActiveTasks = Math.max(activeTaskCount - 2, 1);
            const prevRemainingMinutes = Math.max(remainingMinutes + 180, 60);

            renderTrend(
                document.getElementById('activeTasksTrend'),
                calculateTrend(activeTaskCount, prevActiveTasks)
            );

            renderTrend(
                document.getElementById('remainingHoursTrend'),
                calculateTrend(remainingMinutes, prevRemainingMinutes)
            );

            // Son 7 gün için active task sayısı (basit dağıtım)
            const activeTasksSeries = [
                Math.max(activeTaskCount - 2, 0),
                Math.max(activeTaskCount - 1, 0),
                activeTaskCount,
                activeTaskCount + 1,
                activeTaskCount,
                Math.max(activeTaskCount - 1, 0),
                activeTaskCount
            ];

            // Radial chart percentage based on dynamic capacity
            const remainingCapacityPercent = capacityMinutes > 0
                ? Math.round((remainingMinutes / capacityMinutes) * 100)
                : 0;

            window.timesheetApp.updateActiveTasksChart(activeTasksSeries);
            window.timesheetApp.updateRemainingCapacityChart(remainingCapacityPercent);

            console.log('📊 KPI RESULT', {
                activeTaskCount,
                totalSpentMinutes,
                remainingMinutes,
                remainingHours
            });
        })
        .catch(err => console.error('❌ KPI load error:', err));
}


document.addEventListener('DOMContentLoaded', function () {
    window.timesheetApp = {
        role: 'Employee', // Default
        currentUserId: window.currentUserId,
        dtQuickTimer: null,
        runningTimerId: null,
        timerInterval: null,
        timerSeconds: 0,
        runningSlotId: null,
        runningTaskStartUtc: null,
        timeEntryToDeleteId: null,
        charts: {},
        chartData: {
            activeTasksTrend: [],
            remainingCapacityPercent: 0
        },
        updateActiveTasksChart: function (seriesData) {
            const options = {
                series: [{ data: seriesData }],
                chart: {
                    type: 'bar',
                    height: 60,
                    sparkline: { enabled: true }
                },
                plotOptions: {
                    bar: {
                        columnWidth: '60%',
                        borderRadius: 2
                    }
                },
                colors: [this.colors.info],
                tooltip: { enabled: false }
            };

            this.updateChart('chart-active-tasks', options);
        },
        updateRemainingCapacityChart: function (percent) {
            const safePercent = Math.min(Math.max(percent, 0), 100);

            const options = {
                series: [safePercent],
                chart: {
                    type: 'radialBar',
                    height: 120,
                    sparkline: { enabled: true }
                },
                plotOptions: {
                    radialBar: {
                        hollow: { size: '60%' },
                        dataLabels: {
                            name: { show: false },
                            value: {
                                offsetY: 5,
                                fontSize: '14px',
                                fontWeight: '600',
                                formatter: val => `${val}%`
                            }
                        }
                    }
                },
                colors: [
                    safePercent > 60
                        ? this.colors.success
                        : safePercent > 30
                            ? this.colors.warning
                            : this.colors.error
                ]
            };

            this.updateChart('chart-remaining-capacity', options);
        },
        openDeleteTimeEntryModal: function (timeTrackerId) {
            if (!timeTrackerId) return;

            this.timeEntryToDeleteId = timeTrackerId;

            const modalEl = document.getElementById('deleteConfirmModal');
            if (!modalEl) return;

            const modal = new bootstrap.Modal(modalEl);
            modal.show();
        },
        confirmDeleteTimeEntry: async function () {

            if (!this.timeEntryToDeleteId) return;

            const $btn = $('#confirmDeleteBtn');
            const originalText = $btn.html();

            // 🔒 Disable + spinner
            $btn
                .prop('disabled', true)
                .html('<span class="spinner-border spinner-border-sm me-2"></span>Deleting...');

            const payload = {
                timeTrackerId: this.timeEntryToDeleteId,
                createdBy: window.getUserName()
            };

            try {
                const res = await fetch(`${API.ppm}/TimeSheet/DeleteTimeEntry`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                const result = await res.json();

                // ❌ API Error
                if (!res.ok || result?.errors?.length) {
                    const msg = result?.errors?.[0] || 'Failed to delete time entry';
                    this.showToast(msg, 'error');
                    return;
                }

                // ✅ SUCCESS
                this.showToast('Time entry deleted successfully', 'success');

                // 🔄 UI refresh
                if (window.dtTimeEntries) {
                    window.dtTimeEntries.ajax.reload(null, false);
                }

                loadTimesheetKpis();

                if (typeof this.loadRecentTimeEntries === 'function') {
                    this.loadRecentTimeEntries();
                }

                this.closeDeleteTimeEntryModal();

            } catch (err) {
                console.error('❌ DeleteTimeEntry error:', err);
                this.showToast('Unexpected error while deleting', 'error');
            } finally {
                // 🔓 Reset
                this.timeEntryToDeleteId = null;
                $btn.prop('disabled', false).html(originalText);
            }
        },
        closeDeleteTimeEntryModal: function () {
            const modalEl = document.getElementById('deleteConfirmModal');
            const modalInstance = bootstrap.Modal.getInstance(modalEl);
            modalInstance?.hide();
        },

        // --- CREATE & LOG FEATURE ---
        openCreateAndLogModal: function () {
            const modalEl = document.getElementById('createTaskAndLogModal');
            if (!modalEl) return;

            // 1. Reset Form
            const form = document.getElementById('createTaskAndLogForm');
            form.reset();
            form.classList.remove('was-validated');

            // 2. Set Defaults
            const dateInput = document.getElementById('cl_entryDate');
            dateInput.valueAsDate = new Date(); // Today
            dateInput.min = this.getMinDateIso();
            dateInput.max = this.getMaxDateIso();
            $('#cl_entryDateWarning').addClass('d-none');
            $('#btnCreateAndLogConfirm').prop('disabled', false);

            $('#cl_projectSelect').val(null).trigger('change');

            // 4. Show Modal
            const modal = new bootstrap.Modal(modalEl);
            this.initCreateTaskAndLogTimeDropdown();
            modal.show();
        },


        initCreateTaskAndLogTimeDropdown: function () {
            const $timeSelect = $('#cl_entryTime');
            $timeSelect.empty();

            const options = [];
            let selectedTime = null;
            const now = new Date();
            const currentTotalMinutes = now.getHours() * 60 + now.getMinutes();
            let minDiff = Infinity;

            for (let h = 0; h < 24; h++) {
                for (let m = 0; m < 60; m += 15) {
                    const timeStr = `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`;
                    const minutes = h * 60 + m;

                    options.push({ id: timeStr, text: timeStr });

                    const diff = Math.abs(currentTotalMinutes - minutes);
                    if (diff < minDiff) {
                        minDiff = diff;
                        selectedTime = timeStr;
                    }
                }
            }

            $timeSelect.select2({
                dropdownParent: $('#createTaskAndLogModal'),
                width: '100%',
                data: options
            });

            if (selectedTime) {
                $timeSelect.val(selectedTime).trigger('change');
            }
        },

        submitCreateAndLog: async function () {
            const dateVal = $('#cl_entryDate').val();
            if (!this.isLogDateValid(dateVal)) {
                this.showToast("You can only log time up to 2 days in the past.", "error");
                return;
            }

            const entryTime = $('#cl_entryTime').val();
            const durationVal = $('#cl_duration').val();
            const durationInMinutes = parseInt(durationVal, 10);

            if (isNaN(durationInMinutes) || durationInMinutes <= 0) {
                this.showToast('Duration must be greater than 0', 'error');
                return;
            }

            // 🛑 OVERLAP CHECK
            const isOverlap = await this.checkOverlap(this.currentUserId, dateVal, entryTime, durationInMinutes);
            if (isOverlap) {
                this.showToast("This time range overlaps with another logged task.", "error");
                return;
            }

            const form = document.getElementById('createTaskAndLogForm');
            if (!form.checkValidity()) {
                form.classList.add('was-validated');
                return;
            }

            const $btn = $('#btnCreateAndLogConfirm');
            const originalText = $btn.html();
            $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');

            const dateValCurrent = dateVal; // dateVal is already declared at the top of the function
            const startDateTimeStr = `${dateValCurrent}T${entryTime}:00`;
            const startDate = new Date(startDateTimeStr);
            const startUtc = startDate.toISOString();

            const endDate = new Date(startDate.getTime() + durationInMinutes * 60000);
            const endUtc = endDate.toISOString();
            const userName = window.getUserName();


            const payload = {
                taskTitle: $('#cl_taskTitle').val(),
                startUtc: startUtc,
                endUtc: endUtc,
                //durationMinutes: Number($('#cl_duration').val()),
                description: $('#cl_description').val(),
                userId: this.currentUserId,
                createdBy: userName
            };

            console.log('🚀 Submitting Create & Log:', payload);

            try {
                // Proposed Endpoint
                const res = await fetch(`${API.ppm}/TimeSheet/CreateTaskAndLog`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                const result = await res.json();

                if (!res.ok || result.errors) {
                    throw new Error(result.errors?.[0] || 'Operation failed');
                }

                this.showToast('Task created and time logged successfully!', 'success');

                // Close Modal
                const modalEl = document.getElementById('createTaskAndLogModal');
                const modal = bootstrap.Modal.getInstance(modalEl);
                modal.hide();

                // Refresh Data
                if (window.dtTimeEntries) window.dtTimeEntries.ajax.reload(null, false);
                if (window.dtQuickTimer) window.dtQuickTimer.ajax.reload(null, false);
                loadTimesheetKpis();
                this.loadRecentTimeEntries();

            } catch (err) {
                console.error('❌ Create & Log Error:', err);
                this.showToast(err.message, 'error');
            } finally {
                $btn.prop('disabled', false).html(originalText);
            }
        },
        colors: {
            success: '#10B981',
            warning: '#F59E0B',
            error: '#EF4444',
            info: '#3B82F6',
            gray: '#E5E7EB',
            textPrimary: '#111827',
            textSecondary: '#6B7280'
        },

        // --- VALIDATION HELPERS ---
        isLogDateValid: function (dateValue) {
            if (!dateValue) return false;
            const today = new Date();
            today.setHours(0, 0, 0, 0);

            const minDate = new Date(today);
            minDate.setDate(today.getDate() - 2);

            const [y, m, d] = dateValue.split('-').map(Number);
            const selected = new Date(y, m - 1, d);
            selected.setHours(0, 0, 0, 0);

            return selected >= minDate && selected <= today;
        },

        validateLogDate: function (inputId, warningId, btnId) {
            const val = document.getElementById(inputId)?.value;
            const isValid = this.isLogDateValid(val);
            const $warning = $(`#${warningId}`);
            const $btn = $(`#${btnId}`);

            if (isValid) {
                $warning.addClass('d-none');
                $btn.prop('disabled', false);
            } else {
                $warning.removeClass('d-none');
                $btn.prop('disabled', true);
            }
            return isValid;
        },

        getMinDateIso: function () {
            const d = new Date();
            d.setDate(d.getDate() - 2);
            return this.formatDateToIso(d);
        },

        getMaxDateIso: function () {
            return this.formatDateToIso(new Date());
        },

        formatDateToIso: function (date) {
            const y = date.getFullYear();
            const m = (date.getMonth() + 1).toString().padStart(2, '0');
            const d = date.getDate().toString().padStart(2, '0');
            return `${y}-${m}-${d}`;
        },

        // --- OVERLAP HELPERS ---
        getEntriesForDay: async function (userId, dateStr) {
            const payload = {
                draw: 1,
                start: 0,
                length: 1000,
                search: { value: '' },
                employeeId: userId,
                dateFilter: dateStr // Backend supports date string for specific day
            };

            try {
                const res = await fetch(`${API.ppm}/TimeSheet/GetTimeEntries`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                const result = await res.json();
                return result?.data?.data || [];
            } catch (err) {
                console.error('❌ Error fetching entries for overlap check:', err);
                return [];
            }
        },

        hasOverlappingLog: function (newStart, newEnd, existingLogs) {
            const nStart = new Date(newStart).getTime();
            const nEnd = new Date(newEnd).getTime();

            for (const log of existingLogs) {
                const eStart = new Date(log.start).getTime();
                const eEnd = new Date(eStart + (log.durationInMinutes || 0) * 60000).getTime();

                // ÇAKIŞMA KURALI: newStart < existingEnd AND newEnd > existingStart
                if (nStart < eEnd && nEnd > eStart) {
                    return true;
                }
            }
            return false;
        },

        checkOverlap: async function (userId, dateVal, startTime, durationMinutes) {
            const startDateTimeStr = `${dateVal}T${startTime}:00`;
            const newStart = new Date(startDateTimeStr);
            const newEnd = new Date(newStart.getTime() + durationMinutes * 60000);

            const existingLogs = await this.getEntriesForDay(userId, dateVal);
            return this.hasOverlappingLog(newStart, newEnd, existingLogs);
        },

        init: function () {
            console.log('🚀 Enterprise Timesheet System Initialized');
            this.bindEvents();
            this.loadEmployeeData();
            this.loadEmployeeContext();
            initQuickTimerTable();
            if (typeof initTimeEntriesTable === 'function') initTimeEntriesTable();
            loadTimesheetKpis();
            this.loadRecentTimeEntries();
            this.initCharts();
            // Start in whatever mode is checked
            this.switchMode(document.querySelector('input[name="modeSwitch"]:checked').id === 'modeManager' ? 'Manager' : 'Employee');
        },

        bindEvents: function () {
            const self = this;

            // View All Recent Entries
            // View All Recent Entries (Strict UX)
            $(document).on('click', '#btnViewAllRecentEntries', function (e) {
                e.preventDefault();

                // 1. Activate Time Entries tab
                // Note: Actual target in HTML is #navs-time-entries-log based on file content
                const tabBtn = document.querySelector('button[data-bs-target="#navs-time-entries-log"]');

                if (tabBtn) {
                    const tab = new bootstrap.Tab(tabBtn);
                    tab.show();
                }

                // 2. Scroll to table (after tab visible)
                setTimeout(() => {
                    const tableEl = document.querySelector('.time-entry-table');
                    if (tableEl) {
                        tableEl.scrollIntoView({
                            behavior: 'smooth',
                            block: 'start'
                        });
                    }

                    // 3. Reload table
                    if (window.dtTimeEntries) {
                        window.dtTimeEntries.ajax.reload(null, false);
                    }
                }, 300);
            });
            // Create & Log Logic
            $('#btnCreateAndLog').on('click', function () {
                self.openCreateAndLogModal();
            });

            $('#btnCreateAndLogConfirm').on('click', function () {
                self.submitCreateAndLog();
            });

            // Log Time Button
            $('#btnLogTime').on('click', function () {
                self.openLogTimeModal();
            });

            // Save Log Time
            $('#btnSaveLogTime').on('click', function () {
                self.saveLogTimeEntry();
            });

            // Delete (dropdown click)
            $(document).on('click', '.delete-record', function () {
                const timeTrackerId = $(this).data('id');
                timesheetApp.openDeleteTimeEntryModal(timeTrackerId);
            });

            // Confirm delete
            $('#confirmDeleteBtn').on('click', function () {
                timesheetApp.confirmDeleteTimeEntry();
            });

            // Mode Switching (Employee vs Manager)
            document.querySelectorAll('input[name="modeSwitch"]').forEach(radio => {
                radio.addEventListener('change', (e) => {
                    const mode = e.target.id === 'modeManager' ? 'Manager' : 'Employee';
                    self.switchMode(mode);
                });
            });

            // Employee Context Selector Change
            $('#employeeContextSelector').on('change', function () {
                const selectedUserId = $(this).val();
                console.log("👤 Selected employee:", selectedUserId);
                if (window.dtQuickTimer) {
                    window.dtQuickTimer.ajax.reload();
                }
                if (window.dtTimeEntries) {
                    window.dtTimeEntries.ajax.reload();
                }
                loadTimesheetKpis();
                self.loadRecentTimeEntries();
            });

            // Date Filter Tab Click
            $(document).on('click', '[data-date-filter]', function () {
                if (window.dtQuickTimer) {
                    window.dtQuickTimer.ajax.reload();
                }
                if (window.dtTimeEntries) {
                    window.dtTimeEntries.ajax.reload();
                }
                loadTimesheetKpis();
            });

            // Date Validation Listeners
            $(document).on('change', '#logTimeDate', function () {
                self.validateLogDate('logTimeDate', 'logTimeDateWarning', 'btnSaveLogTime');
            });
            $(document).on('change', '#cl_entryDate', function () {
                self.validateLogDate('cl_entryDate', 'cl_entryDateWarning', 'btnCreateAndLogConfirm');
            });

            // Quick Timer Table "Start" Button
            $(document).on('click', '.btn-start-task', function () {
                const taskId = $(this).data('task-id');
                const taskName = $(this).data('task-name');
                window.timesheetApp.handleTimerToggle(taskId, taskName);
            });

            // Tab Switching Logic (proof of handling)
            const subTabs = document.querySelectorAll('#timesheetSubTabs button[data-bs-toggle="tab"]');
            subTabs.forEach(tab => {
                tab.addEventListener('shown.bs.tab', (e) => {
                    console.log(`📂 Switched to tab: ${e.target.innerText.trim()}`);
                    // Ensure table is properly redrawn if switching back to quick timer
                    if (e.target.dataset.bsTarget === '#navs-quick-timer') {
                        if (self.dtQuickTimer) self.dtQuickTimer.columns.adjust().responsive.recalc();
                    }
                });
            });
        },

        switchMode: function (mode) {
            this.role = mode;
            console.log(`🔄 Switching to ${mode} Mode`);

            const employeeDashboard = document.getElementById('employeeDashboard');
            //const managerDashboard = document.getElementById('managerDashboard');
            const employeeSection = document.getElementById('employeeSection');
            const managerSection = document.getElementById('managerSection');

            if (mode === 'Manager') {
                employeeDashboard.classList.add('d-none');
                employeeSection.classList.add('d-none');
                //managerDashboard.classList.remove('d-none');
                managerSection.classList.remove('d-none');
                this.loadManagerData();
            } else {
                //managerDashboard.classList.add('d-none');
                managerSection.classList.add('d-none');
                employeeDashboard.classList.remove('d-none');
                employeeSection.classList.remove('d-none');
                this.loadEmployeeData();
            }

            // Re-render charts for visible section
            this.initCharts();
        },

        initCharts: function () {
            if (this.role === 'Employee') {
                this.renderEmployeeCharts();
            } else {
                this.renderManagerCharts();
            }
        },

        // --- DASHBOARD CHARTS ---

        renderEmployeeCharts: function () {
            // 1. Active Tasks Mini Bar
            const barOptions = {
                series: [{ data: [12, 18, 15, 20, 10, 8, 14] }],
                chart: { type: 'bar', height: 60, sparkline: { enabled: true } },
                plotOptions: { bar: { columnWidth: '60%', borderRadius: 2 } },
                colors: [this.colors.info],
                tooltip: { enabled: false }
            };
            this.updateChart('chart-active-tasks', barOptions);

            // 2. Remaining Capacity Radial
            const radialOptions = {
                series: [62],
                chart: { type: 'radialBar', height: 120, sparkline: { enabled: true } },
                plotOptions: {
                    radialBar: {
                        hollow: { size: '60%' },
                        dataLabels: {
                            name: { show: false },
                            value: { offsetY: 5, fontSize: '14px', fontWeight: '600', color: this.colors.textPrimary, formatter: (val) => val + '%' }
                        }
                    }
                },
                colors: [this.colors.success]
            };
            this.updateChart('chart-remaining-capacity', radialOptions);
        },

        renderManagerCharts: function () {
            // 1. Team Total Bars (Stacked Horizontal representation mock)
            const teamBarOptions = {
                series: [{ name: 'Billable', data: [44, 55, 41, 67, 22, 43, 21] }, { name: 'Non-Billable', data: [13, 23, 20, 8, 13, 27, 33] }],
                chart: { type: 'bar', height: 60, stacked: true, sparkline: { enabled: true } },
                plotOptions: { bar: { horizontal: false, columnWidth: '60%' } },
                colors: [this.colors.info, this.colors.gray],
                tooltip: { enabled: false }
            };
            this.updateChart('chart-team-bars', teamBarOptions);

            // 2. Overdue Trend line
            const trendOptions = {
                series: [{ data: [5, 4, 6, 3, 5, 2, 5] }],
                chart: { type: 'line', height: 60, sparkline: { enabled: true } },
                stroke: { width: 2, curve: 'smooth' },
                colors: [this.colors.warning],
                tooltip: { enabled: false }
            };
            this.updateChart('chart-overdue-trend', trendOptions);

            // 3. Manager Gauge (Capacity vs Actual)
            const managerRadialOptions = {
                series: [87, 100],
                chart: { type: 'radialBar', height: 140, sparkline: { enabled: true } },
                plotOptions: {
                    radialBar: {
                        dataLabels: {
                            total: {
                                show: true,
                                label: 'Plan vs Act',
                                formatter: () => '87%'
                            }
                        }
                    }
                },
                colors: [this.colors.success, this.colors.gray],
                labels: ['Actual', 'Plan']
            };
            this.updateChart('chart-manager-gauge', managerRadialOptions);

            // 4. Project Distribution (Main Section)
            const projectDistOptions = {
                series: [{
                    data: [
                        { x: 'Project Alpha', y: 45 },
                        { x: 'Project Beta', y: 28 },
                        { x: 'Operations', y: 22 },
                        { x: 'Internal', y: 18 }
                    ]
                }],
                chart: { type: 'bar', height: 250, toolbar: { show: false } },
                plotOptions: { bar: { horizontal: true, distributed: true, borderRadius: 4 } },
                colors: [this.colors.info, '#6366F1', '#8B5CF6', '#A1ACB8'],
                dataLabels: { enabled: true, formatter: (val) => val + 'h' },
                xaxis: { categories: ['Project Alpha', 'Project Beta', 'Operations', 'Internal'] },
                legend: { show: false }
            };
            this.updateChart('chart-project-distribution', projectDistOptions);
        },

        //initQuickTimerTable: function () {
        //    const self = this;
        //    const tableEl = $('.quick-timer-table');

        //    if ($.fn.DataTable.isDataTable(tableEl)) {
        //        tableEl.DataTable().destroy();
        //    }

        //    this.dtQuickTimer = tableEl.DataTable({
        //        pageLength: 100,
        //        lengthMenu: [10, 25, 50, 100],
        //        serverSide: true,
        //        processing: true,
        //        destroy: true,
        //        ajax: function (data, callback, settings) {
        //            const employeeId = $('#employeeContextSelector').val() || self.currentUserId;
        //            const dateFilter = $('[data-date-filter].active').data('date-filter') || 'today';

        //            const payload = {
        //                employeeId: employeeId,
        //                dateFilter: dateFilter,
        //                draw: data.draw,
        //                start: data.start,
        //                length: data.length,
        //                search: {
        //                    value: data.search.value
        //                }
        //            };

        //            $.ajax({
        //                url: `${API.ppm}/TimeSheet/GetQuickTimerTasks`,
        //                type: 'POST',
        //                contentType: 'application/json',
        //                data: JSON.stringify(payload),
        //                success: function (response) {
        //                    if (response && response.data) {
        //                        // Manual mapping as requested
        //                        callback({
        //                            draw: response.data.draw || data.draw,
        //                            recordsTotal: response.data.recordsTotal || 0,
        //                            recordsFiltered: response.data.recordsFiltered || 0,
        //                            data: response.data.data || []
        //                        });
        //                    } else {
        //                        callback({ draw: data.draw, recordsTotal: 0, recordsFiltered: 0, data: [] });
        //                    }
        //                },
        //                error: function (xhr, error, thrown) {
        //                    console.error("❌ Error fetching Quick Timer tasks:", error, thrown);
        //                    callback({ draw: data.draw, recordsTotal: 0, recordsFiltered: 0, data: [] });
        //                }
        //            });
        //        },
        //        columns: [
        //            {
        //                className: 'control',
        //                orderable: false,
        //                defaultContent: '',
        //                targets: 0
        //            },
        //            {
        //                data: 'taskName',
        //                render: function (data, type, row) {
        //                    return `
        //                        <div>
        //                            <div class="fw-bold text-heading">${row.taskName}</div>
        //                            <small class="text-muted">${row.projectName || row.workflowName || 'General Task'}</small>
        //                        </div>`;
        //                }
        //            },
        //            {
        //                data: 'statusName',
        //                render: function (data) {
        //                    let badgeClass = 'bg-label-secondary';
        //                    if (data === 'In Progress') badgeClass = 'bg-label-primary';
        //                    else if (data === 'Completed') badgeClass = 'bg-label-success';
        //                    return `<span class="badge ${badgeClass}">${data}</span>`;
        //                }
        //            },
        //            {
        //                data: 'priorityName',
        //                render: function (data) {
        //                    let badgeClass = 'bg-label-secondary';
        //                    if (data === 'High' || data === 'Critical') badgeClass = 'bg-label-danger';
        //                    else if (data === 'Medium') badgeClass = 'bg-label-warning';
        //                    else if (data === 'Low') badgeClass = 'bg-label-info';
        //                    return `<span class="badge ${badgeClass}">${data}</span>`;
        //                }
        //            },
        //            {
        //                data: 'spentHours',
        //                render: function (data, type, row) {
        //                    return `<span class="fw-bold">${row.spentHours || 0}h</span> / ${row.estimatedHours || 0}h`;
        //                }
        //            },
        //            {
        //                data: 'taskId',
        //                orderable: false,
        //                className: 'text-center',
        //                render: function (data, type, row) {
        //                    const disabled = row.isRunning ? 'disabled' : '';
        //                    return `
        //                        <button class="btn btn-sm btn-icon btn-primary rounded-circle btn-start-task" data-task-id="${data}" ${disabled}>
        //                            <i class="bx bx-play fs-4"></i>
        //                        </button>`;
        //                }
        //            }
        //        ],
        //        order: [], // Default no ordering
        //        responsive: {
        //            details: {
        //                display: $.fn.dataTable.Responsive.display.childRow,
        //                type: 'column',
        //                targets: 0
        //            }
        //        },
        //        dom: '<"row"<"col-sm-12"tr>><"row"<"col-sm-12 col-md-5"i><"col-sm-12 col-md-7"p>>', // Custom DOM for minimal look
        //    });
        //},

        updateChart: function (containerId, options) {
            if (this.charts[containerId]) {
                this.charts[containerId].destroy();
            }
            const el = document.getElementById(containerId);
            if (el) {
                this.charts[containerId] = new ApexCharts(el, options);
                this.charts[containerId].render();
            }
        },

        // --- TIMER LOGIC ---

        handleTimerToggle: function (taskId, taskName) {
            if (this.runningTimerId === taskId) {
                this.stopTimer();
                return;
            }

            // Başka task çalışıyorsa → önce stop
            if (this.runningTimerId) {
                this.stopTimer();
            }

            // Yeni task başlat
            this.startTimer(taskId, taskName);
        },

        startTimer: function (taskId, taskName) {
            this.runningSlotId = taskId;
            this.runningTimerId = taskId;
            this.activeTaskName = taskName;
            this.runningTaskStartUtc = new Date().toISOString(); // 🔥 kritik
            this.timerSeconds = 0; // In real app, fetch from backend or state
            const self = this;

            this.timerInterval = setInterval(() => {
                self.timerSeconds++;
                self.updateTimerDisplay();
            }, 1000);
            const link = document.getElementById('activeTaskLink');
            link.textContent = taskName;
            link.dataset.taskId = taskId;
            link.classList.remove('d-none');

            document.getElementById('runningTimerDisplay').classList.add('text-danger');
            this.showToast(`Timer started for task #${taskName}`, 'success');
            // 🔄 Table refresh (ikon / buton state değişsin)
            if (window.dtQuickTimer) {
                window.dtQuickTimer.ajax.reload(null, false);
            }
        },

        stopTimer: async function () {

            if (!this.runningSlotId || !this.runningTaskStartUtc) {
                console.warn('⛔ No active timer to stop');
                return;
            }

            const endUtc = new Date().toISOString();

            const payload = {
                slotId: this.runningSlotId,
                start: this.runningTaskStartUtc,
                end: endUtc,
                userId: this.currentUserId
            };

            console.log('📤 CreateTimesheetEntry payload', payload);

            try {
                const res = await fetch(`${API.ppm}/Task/CreateTimesheetEntry`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                const result = await res.json();

                if (!res.ok || result.errors) {
                    throw new Error(
                        (result.errors && result.errors[0]) ||
                        result.message ||
                        'Timesheet create failed'
                    );
                }
                const createdTimesheetId = result.data;
                console.log('✅ Timesheet created:', createdTimesheetId);
                this.showToast('Timesheet entry saved', 'success');

            } catch (err) {
                console.error('❌ Timesheet API error', err);
                this.showToast('Failed to save timesheet', 'error');
            }

            // ⏹ UI RESET (her durumda)
            clearInterval(this.timerInterval);
            this.timerInterval = null;
            this.runningTimerId = null;
            this.runningSlotId = null;
            this.runningTaskStartUtc = null;
            this.activeTaskName = null;

            document.getElementById('runningTimerDisplay')
                .classList.remove('text-danger');
            document.getElementById('runningTimerDisplay')
                .innerText = '00:00:00';

            const link = document.getElementById('activeTaskLink');
            link.textContent = '-';
            link.classList.add('d-none');

            this.loadRecentTimeEntries();

            if (window.dtQuickTimer) {
                window.dtQuickTimer.ajax.reload(null, false);
            }
            if (window.dtTimeEntries) {
                window.dtTimeEntries.ajax.reload(null, false);
            }
        },

        updateTimerDisplay: function () {
            const h = Math.floor(this.timerSeconds / 3600);
            const m = Math.floor((this.timerSeconds % 3600) / 60);
            const s = this.timerSeconds % 60;
            const formatted = [h, m, s].map(v => v < 10 ? '0' + v : v).join(':');
            document.getElementById('runningTimerDisplay').innerText = formatted;
        },

        // --- DATA MOCKING ---

        loadEmployeeData: function () {
            const tasksList = document.getElementById('planned-tasks-list');
            if (!tasksList) return;

            const mockPlanned = [
                { id: 'UI-402', title: 'Design timesheet KPI cards', status: 'In Progress', priority: 'High', spent: 4.5, est: 6, color: 'info' },
                { id: 'BE-315', title: 'API endpoint optimization', status: 'To Do', priority: 'Medium', spent: 0, est: 4, color: 'gray' },
                { id: 'MEET-12', title: 'Sprint planning session', status: 'Scheduled', priority: 'Medium', spent: 0, est: 2, color: 'warning' }
            ];

            tasksList.innerHTML = mockPlanned.map(task => `
                <div class="task-row d-flex align-items-center px-4 ${this.runningTimerId === task.id ? 'active-timer-row' : ''}">
                    <div class="form-check me-3">
                        <input class="form-check-input rounded-circle" type="checkbox" style="width: 20px; height: 20px;">
                    </div>
                    <div class="flex-grow-1">
                        <div class="d-flex align-items-center gap-2">
                            <small class="text-muted font-monospace fw-medium">${task.id}</small>
                            <span class="fw-medium">${task.title}</span>
                        </div>
                        <div class="d-flex align-items-center gap-3 mt-1">
                            <span class="status-badge bg-label-${task.color}">${task.status}</span>
                            <small class="text-muted"><i class="bx ${this.getPriorityIcon(task.priority)} me-1"></i>${task.priority}</small>
                            <div class="progress-minimal ms-2">
                                <div class="progress-minimal-fill" style="width: ${(task.spent / task.est) * 100}%"></div>
                            </div>
                            <small class="extra-small fw-semibold text-heading">${task.spent}h / ${task.est}h</small>
                        </div>
                    </div>
                    <div class="ms-auto d-flex align-items-center gap-2">
                        <button class="btn btn-sm btn-icon btn-toggle-timer rounded-circle ${this.runningTimerId === task.id ? 'btn-danger' : 'btn-outline-primary'}" data-task-id="${task.id}">
                            <i class="bx ${this.runningTimerId === task.id ? 'bx-stop' : 'bx-play'} fs-4"></i>
                        </button>
                        <button class="btn btn-sm btn-icon"><i class="bx bx-dots-vertical-rounded fs-4"></i></button>
                    </div>
                </div>
            `).join('');

            const recentEntries = document.getElementById('recent-entries-list');
            const mockRecent = [
                { title: 'Design KPI cards', hours: '2.5h', type: 'Billable', time: '10:00-12:30' },
                { title: 'Bug fix CODE-89', hours: '1.2h', type: 'Billable', time: '09:00-10:12' },
                { title: 'Team standup', hours: '0.3h', type: 'Internal', time: '08:45-09:03' }
            ];

            recentEntries.innerHTML = mockRecent.map(entry => `
                <div class="d-flex align-items-center justify-content-between p-3 border-bottom border-light">
                    <div>
                        <div class="fw-medium">${entry.title}</div>
                        <small class="text-muted">${entry.time}</small>
                    </div>
                    <div class="text-end">
                        <span class="badge bg-label-info mb-1">${entry.hours}</span>
                        <div class="extra-small text-muted text-uppercase fw-bold">${entry.type}</div>
                    </div>
                </div>
            `).join('');
        },

        loadManagerData: function () {
            const tableBody = document.getElementById('team-activity-body');
            if (!tableBody) return;

            const teamData = [
                { name: 'Sarah Chen', avatar: '1.png', active: '2 ⏱', hours: '38.5h', utilization: 92, status: 'On track', health: 'green' },
                { name: 'Marcus Kim', avatar: '2.png', active: '0', hours: '40.0h', utilization: 100, status: 'At cap', health: 'amber' },
                { name: 'James Wilson', avatar: '3.png', active: '0', hours: '36.0h', utilization: 70, status: '2 overdue', health: 'red' }
            ];

            tableBody.innerHTML = teamData.map(user => `
                <tr class="align-middle border-bottom">
                    <td class="px-4 py-3">
                        <div class="d-flex align-items-center gap-3">
                            <div class="avatar avatar-sm"><span class="avatar-initial rounded-circle bg-label-primary">${user.name.charAt(0)}</span></div>
                            <span class="fw-semibold text-heading">${user.name}</span>
                        </div>
                    </td>
                    <td><span class="badge bg-label-info">${user.active}</span></td>
                    <td class="fw-bold fs-6">${user.hours}</td>
                    <td>
                        <div class="d-flex align-items-center gap-2" style="min-width: 150px;">
                            <div class="progress flex-grow-1" style="height: 6px;">
                                <div class="progress-bar" style="width: ${user.utilization}%"></div>
                            </div>
                            <span class="extra-small fw-bold">${user.utilization}%</span>
                        </div>
                    </td>
                    <td>
                        <div class="d-flex align-items-center gap-2">
                            <span class="health-indicator health-${user.health}"></span>
                            <span class="extra-small fw-semibold text-uppercase">${user.status}</span>
                        </div>
                    </td>
                    <td class="text-end px-4">
                        <button class="btn btn-sm btn-outline-secondary">Details</button>
                    </td>
                </tr>
            `).join('');
        },

        loadEmployeeContext: function () {
            const self = this;
            const $selector = $('#employeeContextSelector');

            // Initialize Select2
            $selector.select2({
                placeholder: $selector.data('placeholder'),
                allowClear: false
            });

            if (!this.currentUserId) return;

            // API Usage: Fetch users for the contextual dropdown
            const url = `${API.legacy.user}/api/PvUser/User/GetUsersByUserId/${this.currentUserId}`;

            $.get(url, function (response) {
                if (response && response.data) {
                    const users = response.data;
                    let options = '';

                    users.forEach(user => {
                        // Default user selection logic: select the logged-in user
                        const isSelected = user.userId == self.currentUserId ? 'selected' : '';
                        options += `<option value="${user.id}" ${isSelected}>${user.fullName}</option>`;
                    });

                    $selector.html(options).trigger('change');
                }
            }).fail(function () {
                console.error("❌ Failed to fetch employees for context selector");
            });
        },

        loadRecentTimeEntries: function () {
            const employeeId = $('#employeeContextSelector').val() || window.currentUserId;

            const payload = {
                employeeId: employeeId,
                dateFilter: 'today', // Always today for this widget as per requirement ? Or maybe 'all' sorted DESC to get actual recent? 
                // Requirements say: "dateFilter her zaman 'today' olacak" 
                // But also "Sadece son 5–6 kayıt gösterilecek" and "Sıralama: Start DESC"
                // If we send 'today', we only get today's entries. 
                // Let's stick to user request: dateFilter = "today"
                draw: 1,
                start: 0,
                length: 6,
                search: { value: '' }
            };

            fetch(`${API.ppm}/TimeSheet/GetTimeEntries`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
                .then(r => r.json())
                .then(res => {
                    const entries = res?.data?.data || [];
                    const container = document.getElementById('recent-entries-list');

                    if (!container) return;

                    if (entries.length === 0) {
                        container.innerHTML = '<div class="p-3 text-center text-muted">No recent entries found today.</div>';
                        return;
                    }

                    const html = entries.map(e => {
                        const durationH = ((e.durationInMinutes || 0) / 60).toFixed(1) + 'h';

                        let timeRange = '';
                        let dateStr = '';

                        if (e.start) {
                            const d = new Date(e.start);
                            const isodate = d.toLocaleDateString('tr-TR'); // dd.MM.yyyy if locale matches or manually format

                            const pad = n => n.toString().padStart(2, '0');
                            dateStr = `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()}`;

                            const hh = pad(d.getHours());
                            const mm = pad(d.getMinutes());

                            // Calculate end time roughly
                            const endD = new Date(d.getTime() + (e.durationInMinutes || 0) * 60000);
                            const endHH = pad(endD.getHours());
                            const endMM = pad(endD.getMinutes());

                            timeRange = `${hh}:${mm} - ${endHH}:${endMM}`;
                        }

                        return `
                        <div class="d-flex align-items-center justify-content-between p-3 border-bottom">
                            <div>
                                <div class="fw-medium text-heading">${e.taskName || 'Unknown Task'}</div>
                                <small class="text-muted">${timeRange}</small>
                            </div>
                            <div class="text-end">
                                <span class="badge bg-label-info">${durationH}</span>
                                <div class="extra-small text-muted fw-semibold mt-1">
                                    ${dateStr}
                                </div>
                            </div>
                        </div>
                     `;
                    }).join('');

                    container.innerHTML = html;
                })
                .catch(err => {
                    console.error('❌ Recent entries error:', err);
                    const container = document.getElementById('recent-entries-list');
                    if (container) container.innerHTML = '<div class="p-3 text-center text-danger">Error loading data.</div>';
                });
        },

        getPriorityIcon: function (priority) {
            switch (priority) {
                case 'High': return 'bx-up-arrow-alt text-danger';
                case 'Medium': return 'bx-minus text-warning';
                case 'Low': return 'bx-down-arrow-alt text-info';
                default: return 'bx-minus';
            }
        },

        showToast: function (msg, type = 'success') {

            const toastEl = document.getElementById('appToast');

            // ❗ Asıl kontrol burada olmalı
            if (!toastEl) {
                console.warn('[Toast] #appToast element not found');
                console.log(`[Toast ${type}] ${msg}`);
                return;
            }

            const toastBody = toastEl.querySelector('.toast-body');
            const toastHeader = toastEl.querySelector('#appToastHeader');

            if (!toastBody || !toastHeader) {
                console.warn('[Toast] toast-body or header missing');
                console.log(`[Toast ${type}] ${msg}`);
                return;
            }

            toastBody.innerHTML = msg;

            // Reset background classes
            toastEl.classList.remove('bg-success', 'bg-danger', 'bg-warning', 'bg-info');

            switch (type) {
                case 'success':
                    toastEl.classList.add('bg-success');
                    toastHeader.textContent = 'Successful';
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
                default:
                    toastEl.classList.add('bg-info');
                    toastHeader.textContent = 'Info';
            }

            const toast = bootstrap.Toast.getOrCreateInstance(toastEl, {
                delay: 3000
            });

            toast.show();
        },

        // --- LOG TIME MODAL ---
        openLogTimeModal: function () {
            const modalEl = document.getElementById('logTimeModal');
            if (!modalEl) return;

            // Reset Fields
            $('#logTimeTaskId').empty().trigger('change');

            const today = new Date();
            const dateInput = document.getElementById('logTimeDate');
            if (dateInput) {
                dateInput.valueAsDate = today;
                dateInput.min = this.getMinDateIso();
                dateInput.max = this.getMaxDateIso();
                $('#logTimeDateWarning').addClass('d-none');
                $('#btnSaveLogTime').prop('disabled', false);
            }

            const durationInput = document.getElementById('logTimeDuration');
            if (durationInput) durationInput.value = '';

            // Load Data & Init Selects
            this.loadLogTimeTasksAndMeetings();
            this.initLogTimeStartTime();

            const modal = new bootstrap.Modal(modalEl);
            modal.show();
        },

        loadLogTimeTasksAndMeetings: async function () {
            try {
                const payload = {
                    filter: {
                        currentUserId: this.currentUserId || window.currentUserId,
                        dueDateFilter: null,
                        priorityIds: null,
                        assignedFromUserIds: null
                    }
                };

                const response = await fetch(`${API.ppm}/Task/CalendarSidebar`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                if (!response.ok) throw new Error('API Error');

                const result = await response.json();
                const data = result.data || {};

                const tasks = data.tasks || [];
                const meetings = data.meetings || [];

                const selectData = [
                    {
                        text: 'Tasks',
                        children: tasks.map(t => ({ id: t.id, text: t.name }))
                    },
                    {
                        text: 'Meetings',
                        children: meetings.map(m => ({ id: m.id, text: m.name }))
                    }
                ];

                $('#logTimeTaskId').select2({
                    dropdownParent: $('#logTimeModal'),
                    width: '100%',
                    placeholder: 'Select a task or meeting...',
                    data: selectData
                });

            } catch (err) {
                console.error('❌ Failed to load tasks/meetings:', err);
            }
        },

        initLogTimeStartTime: function () {
            const $timeSelect = $('#logTimeStartTime');
            $timeSelect.empty();

            const options = [];
            let selectedTime = null;
            const now = new Date();
            const currentTotalMinutes = now.getHours() * 60 + now.getMinutes();
            let minDiff = Infinity;

            for (let h = 0; h < 24; h++) {
                for (let m = 0; m < 60; m += 15) {
                    const timeStr = `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`;
                    const minutes = h * 60 + m;

                    options.push({ id: timeStr, text: timeStr });

                    const diff = Math.abs(currentTotalMinutes - minutes);
                    if (diff < minDiff) {
                        minDiff = diff;
                        selectedTime = timeStr;
                    }
                }
            }

            $timeSelect.select2({
                dropdownParent: $('#logTimeModal'),
                width: '100%',
                data: options
            });

            if (selectedTime) {
                $timeSelect.val(selectedTime).trigger('change');
            }
        },

        closeLogTimeModal: function () {
            const modalEl = document.getElementById('logTimeModal');
            const modal = bootstrap.Modal.getInstance(modalEl);
            if (modal) modal.hide();
        },

        saveLogTimeEntry: async function () {
            const taskId = $('#logTimeTaskId').val();
            const dateVal = document.getElementById('logTimeDate').value; // YYYY-MM-DD

            if (!this.isLogDateValid(dateVal)) {
                this.showToast("You can only log time up to 2 days in the past.", "error");
                return;
            }

            const startTime = $('#logTimeStartTime').val(); // HH:mm
            const durationVal = document.getElementById('logTimeDuration').value; // minutes

            const durationInMinutes = parseInt(durationVal, 10);
            if (isNaN(durationInMinutes) || durationInMinutes <= 0) {
                this.showToast('Duration must be greater than 0', 'error');
                return;
            }

            if (!taskId || !dateVal || !startTime || !durationVal) {
                this.showToast('Please fill all fields', 'error');
                return;
            }

            // 🛑 OVERLAP CHECK
            const isOverlap = await this.checkOverlap(this.currentUserId, dateVal, startTime, durationInMinutes);
            if (isOverlap) {
                this.showToast("This time range overlaps with another logged task.", "error");
                return;
            }

            const startTimeVal = startTime;
            const durationMinutesVal = durationInMinutes;

            // ⏱️ START / END UTC HESABI
            const startDateTimeStr = `${dateVal}T${startTimeVal}:00`;
            const startDate = new Date(startDateTimeStr);
            const startUtc = startDate.toISOString();

            const endDate = new Date(startDate.getTime() + durationMinutesVal * 60000);
            const endUtc = endDate.toISOString();
            const description = $('#logTimeDescription').val() || '';
            const userName = window.getUserName();
            const payload = {
                taskId: taskId,
                startUtc: startUtc,
                endUtc: endUtc,
                description: description,
                createdBy: userName
            };

            try {
                const res = await fetch(`${API.ppm}/TimeSheet/LogTime`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                const result = await res.json();

                // ❌ API Error
                if (!res.ok || result?.errors?.length) {
                    const msg = result?.errors?.[0] || 'Failed to log time';
                    this.showToast(msg, 'error');
                    return;
                }

                // ✅ SUCCESS
                this.showToast('Time entry logged successfully', 'success');

                // 🔄 Time Entries table reload
                if (window.dtTimeEntries) {
                    window.dtTimeEntries.ajax.reload(null, false);
                }
                loadTimesheetKpis();
                this.loadRecentTimeEntries();
                this.closeLogTimeModal();

            } catch (err) {
                console.error('❌ LogTime API error:', err);
                this.showToast('Unexpected error while saving time entry', 'error');
            }
        },

    };

    timesheetApp.init();
});
