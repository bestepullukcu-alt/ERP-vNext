'use strict';

const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const port2 = protocol === 'https:' ? '5055' : '5050';
const userName = window.getUserName();
window.taskOverviewList = [];     // API’den gelen gerçek liste
window.workflowData = null;
let dt_task_table = null;      // DataTable instance
let defaultFilter = null; 
let startPicker, endPicker;

startPicker = flatpickr("#filterDateFrom", {
    dateFormat: "d.m.Y",   // ✔ dd.MM.yyyy formatı
    allowInput: true,
    static: true,
    onChange: function (selectedDates) {

        const start = selectedDates[0];

        // End bundan küçük olamaz
        endPicker.set('minDate', start);

        // Eğer end daha önce seçilmiş ve start > end ise temizle
        const endVal = endPicker.selectedDates[0];
        if (endVal && endVal < start) {
            endPicker.clear();
        }
    }
});

endPicker = flatpickr("#filterDateTo", {
    dateFormat: "d.m.Y",   // ✔ dd.MM.yyyy formatı
    allowInput: true,
    static: true,
    onChange: function (selectedDates) {

        const end = selectedDates[0];

        // Start bundan büyük olamaz
        startPicker.set('maxDate', end);

        // Eğer start daha önce seçilmiş ve start > end ise temizle
        const startVal = startPicker.selectedDates[0];
        if (startVal && startVal > end) {
            startPicker.clear();
        }
    }
});

//------------------------------- Create Task Modal Elements -------------------------------//
let fpMeetingStart, fpMeetingEnd, fpDueDate;
let s2TaskType, s2TaskCategory, s2Assignee, s2Status, s2Priority, s2Workflow, s2MeetingAttendees;
let s2StartTime, s2EndTime;
s2TaskType = $('#add-task-type').select2({ width: '100%' });
s2TaskCategory = $('#add-task-category').select2({ width: '100%' });
s2Assignee = $('#add-task-assignee').select2({ width: '100%' });
s2Status = $('#add-task-status').select2({ width: '100%' });
s2Priority = $('#add-task-priority').select2({ width: '100%' });
s2Workflow = $('#add-task-workflow').select2({ width: '100%' });

s2StartTime = $('#add-meeting-start-time').select2({ width: '100%' });
s2EndTime = $('#add-meeting-end-time').select2({ width: '100%' });

// Bootstrap-select init (global)
s2MeetingAttendees = $('#add-meeting-attendees').select2({
    width: "100%",
    placeholder: "Select attendees",
    allowClear: true,
    closeOnSelect: false // multiple seçim için daha iyi UX
});

// Flatpickr init (global sakla)
fpMeetingStart = flatpickr("#add-meeting-start-date", {
    dateFormat: "d.m.Y",   // ✔ dd.MM.yyyy formatı
    allowInput: true,
    static: true,
    onChange: function (selectedDates) {

        const start = selectedDates[0];

        // End bundan küçük olamaz
        fpMeetingEnd.set('minDate', start);

        // Eğer end daha önce seçilmiş ve start > end ise temizle
        const endVal = fpMeetingEnd.selectedDates[0];
        if (endVal && endVal < start) {
            fpMeetingEnd.clear();
        }
        validateMeetingTimes();
    }
});

fpMeetingEnd = flatpickr("#add-meeting-end-date", {
    dateFormat: "d.m.Y",   // ✔ dd.MM.yyyy formatı
    allowInput: true,
    static: true,
    onChange: function (selectedDates) {

        const end = selectedDates[0];

        // Start bundan büyük olamaz
        fpMeetingStart.set('maxDate', end);

        // Eğer start daha önce seçilmiş ve start > end ise temizle
        const startVal = fpMeetingStart.selectedDates[0];
        if (startVal && startVal > end) {
            fpMeetingStart.clear();
        }
        validateMeetingTimes();
    }
});

fpDueDate = flatpickr("#add-task-due-date", {
    dateFormat: "d.m.Y",
    allowInput: true,
    static: true,
    minDate: "today"  // ✔ Bugünden önce seçim yapılamaz
});
let fvCreateTask = null;
const txtTaskEstimated = document.querySelector("#txt-task-estimated-hour");

//-------------------------- Full Form Elements Init --------------------------//
let fpFullTaskStart = null;
let fpFullTaskDue = null;
fpFullTaskStart = flatpickr("#dtTaskStartDate", {
    dateFormat: "d.m.Y",
    allowInput: true,
    static: true,
    minDate: "today",     // Start < today olamaz
    onChange: function (selectedDates) {
        const start = selectedDates[0];

        if (fpFullTaskDue) {
            fpFullTaskDue.set("minDate", start);

            // Eğer mevcut dueDate, start'tan küçükse temizle
            const dueVal = fpFullTaskDue.selectedDates[0];
            if (dueVal && dueVal < start) {
                fpFullTaskDue.clear();
            }

        }

        // 🔥 SUBTASK → START / DUE MIN UPDATE
        if (fpSubTaskStart) fpSubTaskStart.set("minDate", start);
        if (fpSubTaskDue) fpSubTaskDue.set("minDate", start);
    }
});
fpFullTaskDue = flatpickr("#dtTaskDueDate", {
    dateFormat: "d.m.Y",
    allowInput: true,
    static: true,
    minDate: "today",
    onChange: function (selectedDates) {
        const due = selectedDates[0];

        if (fpFullTaskStart) {
            fpFullTaskStart.set("maxDate", due);

            // Eğer start > due ise temizle
            const startVal = fpFullTaskStart.selectedDates[0];
            if (startVal && startVal > due) {
                fpFullTaskStart.clear();
            }
        }

        // 🔥 SUBTASK → START / DUE MAX UPDATE
        if (fpSubTaskStart) fpSubTaskStart.set("maxDate", due);
        if (fpSubTaskDue) fpSubTaskDue.set("maxDate", due);
    }
});
//-------------------------- Full Form SubTask Elements Init --------------------------//
let fpSubTaskStart, fpSubTaskDue;
const subHourInput = document.querySelector("#txt-sub-task-estimated-hour");
let subTaskList = [];
//-------------------------- Full Form Dependency Elements Init --------------------------//
let dependenciesTasks = [];
let fvDependenciesTask = null;

//------------------------------- Full Form Checklist Elements -------------------------------//
let checklistTasks = [];
let fvChecklistTask = null;

//------------------------------- Full Form Meeting Elements -------------------------------//
let fpMeetingStartFull, fpMeetingEndFull;
fpMeetingStartFull = flatpickr("#txt-meeting-start-date", {
    dateFormat: "d.m.Y",
    allowInput: true,
    static: true,
    onChange: function (selectedDates) {

        const start = selectedDates[0];

        // End bundan küçük olamaz
        fpMeetingEndFull.set("minDate", start);

        const endVal = fpMeetingEndFull.selectedDates[0];
        if (endVal && endVal < start) {
            fpMeetingEndFull.clear();
        }

        validateMeetingTimesFull();
    }
});
fpMeetingEndFull = flatpickr("#txt-meeting-end-date", {
    dateFormat: "d.m.Y",
    allowInput: true,
    static: true,
    onChange: function (selectedDates) {

        const end = selectedDates[0];

        fpMeetingStartFull.set("maxDate", end);

        const startVal = fpMeetingStartFull.selectedDates[0];
        if (startVal && startVal > end) {
            fpMeetingStartFull.clear();
        }

        validateMeetingTimesFull();
    }
});
//------------------------------- Meeting Elements End -------------------------------//
let agendaList = [];



document.addEventListener("DOMContentLoaded",async function () {

    defaultFilter = {
        currentUserId: window.getUserId(),   // bunu login user id ile set etmelisin
        mode: "assigned",                      // bana atananlar
        statusIds: null,
        priorityIds: null,
        workflowIds: null,
        startDateFrom: null,
        startDateTo: null,
        search: null
    };

    refreshTaskOverview();
    await initTaskOverviewFilterUI();
    initializeCreateTaskValidation();
    fvDependenciesTask = initializeDependenciesTaskValidation();
    fvChecklistTask = initializeChecklistTaskValidation();
});

async function loadTaskOverview(filterModel) {
    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskOverview`;

        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(filterModel)
        });

        const result = await response.json();

        // ✔ success alanı yok → data kontrolü ile ilerle
        if (result && result.data) {
            taskOverviewList = result.data;
            initTaskOverviewDataTable();
        } else {
            console.error("Overview API error:", result);
        }

    } catch (err) {
        console.error("Overview API exception:", err);
    }
}

function initTaskOverviewDataTable() {
    const tableElem = document.querySelector('.workflow-task-list-table');
    if (!tableElem) return;

    const formatDate = d =>
        d ? new Date(d).toLocaleDateString('tr-TR') : '';

    const formatTime = d =>
        new Date(d).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });

    const getRuntimeSlots = row =>
        Array.isArray(row.runtimeSlots)
            ? row.runtimeSlots
                .map(s => ({ start: new Date(s.start), end: new Date(s.end) }))
                .filter(s => !isNaN(s.start) && !isNaN(s.end))
            : [];

    const minutesBetween = (a, b) =>
        Math.max(0, Math.round((b - a) / 60000));

    // Eğer DataTable daha önce oluşturulmamışsa
    if (!dt_task_table) {

        dt_task_table = new DataTable(tableElem, {
            data: taskOverviewList,
            columns: [
                { data: 'id' },          // hidden
                {
                    data: 'name',
                    title: 'Name',
                },                       
                { data: 'typeName' },
                { data: 'categoryName' },
                { data: 'workflowName' },
                { data: null },
                { data: 'priorityName' },
                { data: 'progress' },
                {data: null },
                {
                    data: null,
                    render: (data, type, row) => {
                        const formattedActual = formatDuration(row.actualHour);  // saniye → h/m/s
                        return `${formattedActual} / ${row.estimatedHour}m`;
                    }
                },
                { data: 'statusName' },
                {
                    data: null,
                    responsivePriority:1,
                    orderable: false,
                    render: renderTaskActions
                },
                {
                    data: null,
                    title: 'Name',

                },
                {
                    data: 'categoryName',
                    title: 'Category',

                },
                {
                    data: 'priorityName',
                    title: 'Priority',

                },
                {
                    data: 'workflowName',
                    title: 'Workflow',

                },
                { data: 'estimatedHour', title: 'Estimated Time (min)' },     // 16 Estimated Time
                { data: 'startDate', title: 'Start Date' },         // 17 Start Date
                { data: 'endDate', title: 'Due Date' },           // 18 Due Date
                { data: null, title: 'Schedule Date' },                // 19 Schedule Date
                { data: null, title: 'Schedule Time' },                // 20 Schedule Time
                { data: null, title: 'Assignees' },                // 21 Assignees
                { data: null, title: 'Task Type' },                // 22 Task / Subtask
                { data: null, title: 'Parent Task' },                // 23 Parent Task
                { data: null, title: 'Planned Sessions' },                // 24 Planned Sessions
                { data: null, title: 'Task Schedule Total' },                // 25 Task Schedule Total
                { data: null, title: 'Overlap Detected' },             // 26 Overlap Detected
                { data: 'statusName', title: 'Status' }
            ],
            columnDefs: [
                {
                    className: 'control',
                    responsivePriority: 2,
                    searchable: false,
                    targets: 0,
                    render: () => ''
                },
                {
                    targets: [12,13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,12,24,25,26,27],
                    visible: false,
                    searchable: false
                },
                {
                    targets: 17,
                    render: (_, __, row) => {
                        return formatDate(row.startDate);


                    }
                },
                {
                    targets: 18,
                    render: (_, __, row) => {
                        return formatDate(row.endDate);

                       
                    }
                },
                {
                    targets: 19,
                    render: (_, __, row) => {
                        if (row.typeId === 2)
                            return formatDate(row.startDate);

                        const slots = getRuntimeSlots(row);
                        return slots.map(s => formatDate(s.start)).join('\n');
                    }
                },
                {
                    targets: 20,
                    render: (_, __, row) => {
                        if (row.typeId === 2 && row.startDate && row.endDate)
                            return `${formatTime(row.startDate)} - ${formatTime(row.endDate)}`;

                        const slots = getRuntimeSlots(row);
                        return slots
                            .map(s => `${formatTime(s.start)} - ${formatTime(s.end)}`)
                            .join('\n');
                    }
                },
                {
                    targets: 21,
                    render: (_, __, row) =>
                        (row.assignee || []).map(a => a.name).join(', ')
                },
                {
                    targets: 22,
                    title: 'Task Type',
                    render: (_, __, row) => {

                        // 1️⃣ Meeting
                        if (Number(row.typeId) === 2) {
                            return 'Meeting';
                        }

                        // 2️⃣ Sub Task
                        if (row.parentTaskId) {
                            return 'Sub Task';
                        }

                        // 3️⃣ Task
                        return 'Task';
                    }
                },
                {
                    targets: 23,
                    render: (_, __, row) =>
                        row.parentTaskId ? (row.parentTaskName || row.parentTaskId) : ''
                },
                {
                    targets: 24,
                    render: (_, __, row) =>
                        getRuntimeSlots(row).length
                },
                {
                    targets: 25,
                    render: (_, __, row) => {
                        const slots = getRuntimeSlots(row);
                        const total = slots.reduce(
                            (sum, s) => sum + minutesBetween(s.start, s.end), 0
                        );
                        if (!total) return '';
                        const h = Math.floor(total / 60);
                        const m = total % 60;
                        return h ? `${h}h ${m}m` : `${m}m`;
                    }
                },
                {
                    targets: 26,
                    render: (_, __, row) => {
                        const slots = getRuntimeSlots(row).sort((a, b) => a.start - b.start);
                        for (let i = 0; i < slots.length - 1; i++) {
                            if (slots[i + 1].start < slots[i].end)
                                return 'Yes';
                        }
                        return 'No';
                    }
                },
                {
                    targets: 12,
                    render: (_, __, row) =>
                        row.name 
                },
                {
                    targets: 1,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        const name = full['name'] || '';

                        // 🔥 EXPORT / FILTER / SORT
                        if (type === 'export' || type === 'filter' || type === 'sort') {
                            return name;
                        }

                        const description = full['description'] || '';

                        const maxDescLength = 50;
                        const maxNameLength = 30;

                        const shortDesc = description.length > maxDescLength
                            ? description.substring(0, maxDescLength) + '...'
                            : description;

                        const shortName = name.length > maxNameLength
                            ? name.substring(0, maxNameLength) + '...'
                            : name;

                        const tooltipAttr = description.length > maxDescLength
                            ? `data-bs-toggle="tooltip" title="${description.replace(/"/g, '&quot;')}"`
                            : '';

                        return `
            <div class="d-flex justify-content-start align-items-center survey-name">
                <div class="d-flex flex-column text-wrap">
                    <span class="fw-medium text-truncate" style="max-width:250px;" ${tooltipAttr}>${shortName}</span>
                    <small class="text-break task-desc text-truncate" style="max-width:250px;" ${tooltipAttr}>${shortDesc}</small>
                </div>
            </div>
        `;
                    }
                },
                {
                    targets: 2,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {

                        const typeId = Number(full.typeId);
                        let color = 'primary'; // Task default

                        if (typeId === 2) color = 'info'; // Meeting

                        return `<span class="badge bg-label-${color}">${data}</span>`;
                    }
                },
                {
                    targets: 3,
                    responsivePriority: 3,
                    render: (data) =>
                        `<span class="badge bg-label-primary">${data}</span>`
                },
                {
                    targets: 5,
                    responsivePriority: 1,
                    render: function (data, type, row) {

                        // Hiç assignee yoksa:
                        if (!Array.isArray(row.assignee) || row.assignee.length === 0) {
                            return `
                <span class="badge bg-label-secondary rounded-circle d-inline-flex align-items-center justify-content-center"
                      style="width:32px; height:32px; font-size:12px;">
                    -
                </span>`;
                        }

                        const colors = ["primary", "success", "warning", "danger", "info", "dark"];

                        return row.assignee.map((a, i) => {
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
                {
                    targets: 6,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        let color = 'secondary';
                        const priority = Number(full['priorityId']);
                        if (priority === 3) color = 'warning';
                        else if (priority === 2) color = 'info';
                        else if (priority === 1) color = 'primary';
                        else if (priority === 4) color = 'danger';
                        return `<span class="badge bg-label-${color}">${data}</span>`;
                    }
                },
                {
                    targets: 7,
                    responsivePriority: 2,
                    render: (data, type, full) => {

                        if (Number(full.typeId) === 2) {
                            // Meeting ise:
                            const start = new Date(full.startDate);
                            const end = new Date(full.endDate);
                            const mins = Math.round((end - start) / 60000);

                            return `<span class="badge bg-label-secondary">
                        <i class="icon-base bx bx-time me-1"></i> ${mins}m
                    </span>`;
                        }

                        // Decimal → Safe Integer
                        let progressValue = Number(full.progress);
                        if (isNaN(progressValue)) progressValue = 0;
                        if (progressValue > 100) progressValue = 100;
                        progressValue = Math.round(progressValue);

                        return `
            <div class="d-flex align-items-center">
                <div class="progress w-100 me-2" style="height: 6px;">
                    <div class="progress-bar bg-primary" role="progressbar"
                        style="width: ${progressValue}%;" 
                        aria-valuenow="${progressValue}" 
                        aria-valuemin="0" aria-valuemax="100">
                    </div>
                </div>
                <small>${progressValue}%</small>
            </div>
        `;
                    }
                },
                {
                    data: null,
                    targets: 8,
                    className:'col-dates', 
                    responsivePriority: 1,
                    render: (data, type, row) => {

                        // Tarihler null gelirse fallback
                        const startRaw = row.startDate ? new Date(row.startDate) : null;
                        const endRaw = row.endDate ? new Date(row.endDate) : null;

                        const safeDate = d => {
                            if (!d || isNaN(d.getTime())) {
                                return {
                                    date: "-",
                                    time: "-"
                                };
                            }

                            const day = String(d.getDate()).padStart(2, '0');
                            const month = String(d.getMonth() + 1).padStart(2, '0');
                            const year = d.getFullYear();

                            const hours = String(d.getHours()).padStart(2, '0');
                            const minutes = String(d.getMinutes()).padStart(2, '0');

                            return {
                                date: `${day}.${month}.${year}`,
                                time: `${hours}:${minutes}`
                            };
                        };

                        const s = safeDate(startRaw);
                        const e = safeDate(endRaw);

                        return `
            <div class="d-flex flex-column">
                <small>S: ${s.date} ${s.time}</small>
                <small>E: ${e.date} ${e.time}</small>
            </div>
        `;
                    }
                },
                {
                    targets: 10,
                    responsivePriority: 1,
                    render: (data, type, full) => {
                        let color = 'secondary';
                        const taskStatusId = Number(full['statusId']);

                        if (taskStatusId === 3) color = 'success';
                        else if (taskStatusId === 2) color = 'info';
                        else if (taskStatusId === 1) color = 'warning';
                        else if (taskStatusId === 4) color = 'danger';
                        return `<span class="badge bg-label-${color}">${data}</span>`;
                    }
                },







            ],
            order: [[1, 'asc']],
            displayLength: 100,
            layout: {
                topStart: {
                    rowClass: 'row m-3 justify-content-between',
                    features: [
                        {
                            pageLength: {
                                menu: [10, 25, 50, 100],
                                text: '_MENU_'
                            },
                        }
                    ]
                },
                topEnd: {
                    rowClass: 'row mx-3 justify-content-between',
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
                                            text: `<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-1"></i>Print</span>`,
                                            className: 'dropdown-item',
                                            exportOptions: {
                                                columns: [1, 2, 3, 4, 5, 6, 7, 9,10,11,12],
                                                format: {
                                                    body: function (inner, coldex, rowdex) {
                                                        if (inner.length <= 0) return inner;

                                                        // Check if inner is HTML content
                                                        if (inner.indexOf('<') > -1) {
                                                            const parser = new DOMParser();
                                                            const doc = parser.parseFromString(inner, 'text/html');

                                                            // Get all text content
                                                            let text = '';

                                                            // Handle specific elements
                                                            const userNameElements = doc.querySelectorAll('.customer-name');
                                                            if (userNameElements.length > 0) {
                                                                userNameElements.forEach(el => {
                                                                    // Get text from nested structure
                                                                    const nameText =
                                                                        el.querySelector('.fw-medium')?.textContent ||
                                                                        el.querySelector('.d-block')?.textContent ||
                                                                        el.textContent;
                                                                    text += nameText.trim() + ' ';
                                                                });
                                                            } else {
                                                                // Get regular text content
                                                                text = doc.body.textContent || doc.body.innerText;
                                                            }

                                                            return text.trim();
                                                        }

                                                        return inner;
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
                                            text: `<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-1"></i>Csv</span>`,
                                            className: 'dropdown-item',
                                            exportOptions: {
                                                columns: [1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12],
                                                format: {
                                                    body: function (inner, coldex, rowdex) {
                                                        if (inner.length <= 0) return inner;

                                                        // Parse HTML content
                                                        const parser = new DOMParser();
                                                        const doc = parser.parseFromString(inner, 'text/html');

                                                        let text = '';

                                                        // Handle customer-name elements specifically
                                                        const userNameElements = doc.querySelectorAll('.customer-name');
                                                        if (userNameElements.length > 0) {
                                                            userNameElements.forEach(el => {
                                                                // Get text from nested structure - try different selectors
                                                                const nameText =
                                                                    el.querySelector('.fw-medium')?.textContent ||
                                                                    el.querySelector('.d-block')?.textContent ||
                                                                    el.textContent;
                                                                text += nameText.trim() + ' ';
                                                            });
                                                        } else {
                                                            // Handle other elements (status, role, etc)
                                                            text = doc.body.textContent || doc.body.innerText;
                                                        }

                                                        return text.trim();
                                                    }
                                                }
                                            }
                                        },
                                        {
                                            extend: 'excel',
                                            title: null,
                                            text: `<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-1"></i>Excel</span>`,
                                            className: 'dropdown-item',
                                            exportOptions: {
                                                columns: [12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 12, 24, 25, 26,27],
                                                format: {
                                                    body: function (inner, coldex, rowdex) {
                                                        if (inner.length <= 0) return inner;

                                                        const parser = new DOMParser();
                                                        const doc = parser.parseFromString(inner, 'text/html');

                                                        let text = '';

                                                        // Handle customer-name elements specifically
                                                        const userNameElements = doc.querySelectorAll('.customer-name');
                                                        if (userNameElements.length > 0) {
                                                            userNameElements.forEach(el => {
                                                                // Get text from nested structure - try different selectors
                                                                const nameText =
                                                                    el.querySelector('.fw-medium')?.textContent ||
                                                                    el.querySelector('.d-block')?.textContent ||
                                                                    el.textContent;
                                                                text += nameText.trim() + ' ';
                                                            });
                                                        } else {
                                                            // Handle other elements (status, role, etc)
                                                            text = doc.body.textContent || doc.body.innerText;
                                                        }

                                                        return text.trim();
                                                    }
                                                }
                                            }
                                        },
                                        {
                                            extend: 'pdf',
                                            text: `<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-pdf me-1"></i>Pdf</span>`,
                                            className: 'dropdown-item',
                                            exportOptions: {
                                                columns: [1, 2, 3, 4, 5, 6, 7, 8],
                                                format: {
                                                    body: function (inner, coldex, rowdex) {

                                                        // 🔥 1️⃣ NAME KOLONU → SADECE NAME
                                                        if (colIdx === 1) {
                                                            // node varsa dataset'ten al (en sağlam yol)
                                                            if (node && node.dataset && node.dataset.name) {
                                                                return node.dataset.name;
                                                            }

                                                            // fallback → HTML içinden sadece ilk satır
                                                            const text = inner.replace(/<[^>]+>/g, '').trim();
                                                            return text.split('\n')[0];
                                                        }


                                                        if (inner.length <= 0) return inner;

                                                        // Parse HTML content
                                                        const parser = new DOMParser();
                                                        const doc = parser.parseFromString(inner, 'text/html');

                                                        let text = '';

                                                        // Handle customer-name elements specifically
                                                        const userNameElements = doc.querySelectorAll('.customer-name');
                                                        if (userNameElements.length > 0) {
                                                            userNameElements.forEach(el => {
                                                                // Get text from nested structure - try different selectors
                                                                const nameText =
                                                                    el.querySelector('.fw-medium')?.textContent ||
                                                                    el.querySelector('.d-block')?.textContent ||
                                                                    el.textContent;
                                                                text += nameText.trim() + ' ';
                                                            });
                                                        } else {
                                                            // Handle other elements (status, role, etc)
                                                            text = doc.body.textContent || doc.body.innerText;
                                                        }

                                                        return text.trim();
                                                    }
                                                }
                                            }
                                        },
                                        {
                                            extend: 'copy',
                                            text: `<i class="icon-base bx bx-copy me-1"></i>Copy`,
                                            className: 'dropdown-item',
                                            exportOptions: {
                                                columns: [1, 2, 3, 4, 5, 6, 7, 8],
                                                format: {
                                                    body: function (inner, coldex, rowdex) {
                                                        if (inner.length <= 0) return inner;

                                                        // Parse HTML content
                                                        const parser = new DOMParser();
                                                        const doc = parser.parseFromString(inner, 'text/html');

                                                        let text = '';

                                                        // Handle customer-name elements specifically
                                                        const userNameElements = doc.querySelectorAll('.customer-name');
                                                        if (userNameElements.length > 0) {
                                                            userNameElements.forEach(el => {
                                                                // Get text from nested structure - try different selectors
                                                                const nameText =
                                                                    el.querySelector('.fw-medium')?.textContent ||
                                                                    el.querySelector('.d-block')?.textContent ||
                                                                    el.textContent;
                                                                text += nameText.trim() + ' ';
                                                            });
                                                        } else {
                                                            // Handle other elements (status, role, etc)
                                                            text = doc.body.textContent || doc.body.innerText;
                                                        }

                                                        return text.trim();
                                                    }
                                                }
                                            }
                                        }
                                    ]

                                },
                                {
                                    text: '<i class="icon-base bx bx-check-circle"></i>',
                                    className: 'toggle-assigned btn btn-icon btn-lg btn-label-primary'
                                },
                                {
                                    text: '<i class="icon-base bx bx-filter-alt"></i>',
                                    className: 'filter-task btn btn-icon btn-lg btn-label-secondary'
                                    
                                },
                                {
                                    text: '<i class="icon-base bx bx-plus"></i><span class="d-none d-sm-inline-block">Create Task</span>',
                                    className: 'btnCreateTask  btn btn-primary'
                                   
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
                            .map(function (col) {
                                return col.title !== '' // ? Do not show row in modal popup if title is blank (for check box)
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

            drawCallback: () => modifyDataTableLayout()
        });
        // ⬇️ Assignee kolonunu assigned modunda gizle
        if (defaultFilter.mode === "assigned") {
            dt_task_table.column(5).visible(false);
        }

    }
    else {
        // Zaten oluşturulmuş → sadece update
        dt_task_table.clear();
        if (defaultFilter.mode === "assigned") {
            dt_task_table.column(5).visible(false);
        }
        else {
            dt_task_table.column(5).visible(true);

        }
        dt_task_table.rows.add(taskOverviewList);
        dt_task_table.draw();
    }
}
function formatDuration(seconds) {
    if (!seconds || seconds <= 0) return "0s";

    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;

    const parts = [];
    if (h > 0) parts.push(`${h}h`);
    if (m > 0) parts.push(`${m}m`);
    if (s > 0) parts.push(`${s}s`);

    return parts.join(" ");
}

function renderTaskActions(row) {
    const id = row.id;
    const name = row.name;
    const description = row.description;
    const isOwner = row.canEditMainFields;
    const isAssignee = row.canChangeStatus && !row.canEditMainFields;
    const isMeeting = row.typeId === 2;

    // ---------------------------------
    // 0) COMPLETED TASK → Only Show
    // ---------------------------------
    if (Number(row.statusId) === 3) {
        return `
        <div class="d-flex align-items-center">

        <a href="javascript:;" class="btn btn-icon show-task" data-id="${id}"> <i class="bx bx-show"></i> </a> </div>`;

    }

    // ---------------------------------
    // 1) OWNER → Edit + Delete + Show
    // ---------------------------------
    if (isOwner) {
        return `
        <div class="d-flex align-items-center">

        <a href="javascript:;" class="btn btn-icon show-task" data-id="${id}"> <i class="bx bx-show"></i> </a>

        <a href="javascript:;" class="btn btn-icon edit-task" data-id="${id}"> <i class="bx bx-edit-alt"></i> </a> <a href="javascript:;" class="btn btn-icon delete-task" data-id="${id}"> <i class="bx bx-trash"></i> </a> </div>`;
    }

    // ---------------------------------
    // 2) ASSIGNEE → MEETING → Only Show
    // ---------------------------------
    if (isAssignee && isMeeting) {
        return `
            <button class="btn btn-sm btn-label-info show-task" data-id="${id}">
                <i class="bx bx-show"></i> Show
            </button>
        `;
    }

    // ---------------------------------
    // 3) ASSIGNEE → TASK → Add Sub/Dep/Check + Show
    // ---------------------------------
    if (isAssignee && !isMeeting) {
        return `
        <div class="btn-group">
           <button class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown">
                <i class="bx bx-dots-vertical-rounded"></i>
            </button>
           <div class="dropdown-menu dropdown-menu-end">

                <a class="dropdown-item show-task" data-id="${id}">
                    <i class="bx bx-show"></i> Show
                </a>

                
                  <hr class="dropdown-divider">
                


                <a class="dropdown-item add-subtask" data-id="${id}" data-name="${name}" data-description="${description}">
                    <i class="bx bx-git-branch"></i> Add Subtask
                </a>

                <a class="dropdown-item add-dependency" data-id="${id}">
                    <i class="bx bx-link"></i> Add Dependency
                </a>

                <a class="dropdown-item add-checklist" data-id="${id}">
                    <i class="bx bx-list-check"></i> Add Checklist
                </a>

            </div>
        </div>`;
    }

    // ---------------------------------
    // 4) READ-ONLY USERS → Only Show
    // ---------------------------------
    return `
        <button class="btn btn-sm btn-label-secondary show-task" data-id="${id}">
            <i class="bx bx-show"></i> Show
        </button>
    `;
}

async function refreshTaskOverview() {
    //const filterModel = collectFilterFormValues(); // senin filter formun
    const filterModel = defaultFilter;
    await loadTaskOverview(filterModel);
    updateTaskDashboardCards();
}
function collectFilterFormValues() {
    return {
        currentUserId: window.currentUserId,
        mode: $("#ddlTaskMode").val(),
        statusIds: $("#ddlStatus").val(),
        priorityIds: $("#ddlPriority").val(),
        workflowIds: $("#ddlWorkflow").val(),
        startDateFrom: $("#txtStartFrom").val(),
        startDateTo: $("#txtStartTo").val(),
        search: $("#txtSearch").val()
    };
}
$(document).on("click", ".meeting-accept", function () {
    const id = $(this).data("id");
    updateMeetingInviteStatus(id, 2);
});

$(document).on("click", ".meeting-maybe", function () {
    const id = $(this).data("id");
    updateMeetingInviteStatus(id, 3);
});

$(document).on("click", ".meeting-decline", function () {
    const id = $(this).data("id");
    updateMeetingInviteStatus(id, 4);
});

async function updateMeetingInviteStatus(taskId, status) {
    await fetch(`/api/meetings/${taskId}/respond`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status })
    });

    refreshTaskOverview();
}

function modifyDataTableLayout() {

    const elementsToModify = [
        { selector: '.dt-buttons .btn', classToRemove: 'btn-secondary' },
        { selector: '.dt-search .form-control', classToRemove: 'form-control-sm' },
        { selector: '.dt-length .form-select', classToRemove: 'form-select-sm', classToAdd: 'ms-0' },
        { selector: '.dt-length', classToAdd: 'mb-md-6 mb-0' },
        { selector: '.dt-search', classToAdd: 'mb-md-6 mb-2' },
        {
            selector: '.dt-layout-end',
            classToRemove: 'justify-content-between',
            classToAdd: 'd-flex gap-md-4 justify-content-md-between justify-content-center gap-4 flex-wrap mt-0'
        },
        { selector: '.dt-layout-start', classToAdd: 'mt-0' },
        { selector: '.dt-buttons', classToAdd: 'd-flex gap-4 mb-md-0 mb-6' },
        { selector: '.dt-layout-table', classToRemove: 'row mt-2' },
        { selector: '.dt-layout-full', classToRemove: 'col-md col-12', classToAdd: 'table-responsive' }
    ];

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

$(document).on('click', '.toggle-assigned', function () {

    if (defaultFilter.mode === "assigned") {

        // MODE -> owned
        defaultFilter.mode = "owned";

        // Görünümü değiştir (aktif değil)
        $(this)
            .removeClass("btn-label-primary")
            .addClass("btn-label-info");

        // İkonu değiştir istersen
        // $(this).html('<i class="bx bx-check-circle"></i>');

    } else {

        // MODE -> assigned
        defaultFilter.mode = "assigned";

        // Görünümü değiştir (aktif)
        $(this)
            .removeClass("btn-label-info")
            .addClass("btn-label-primary");

        // $(this).html('<i class="bx bx-check-circle"></i>');
    }

    // Yeniden yükleme
    refreshTaskOverview();
});

$(document).on('click', '.filter-task', async function () {

    // 1) Dropdownları doldur

    // 2) Offcanvas'ı aç
    const canvas = new bootstrap.Offcanvas('#offcanvasTaskOverviewFilters');
    canvas.show();
});
async function initTaskOverviewFilterUI() {


    // 1) Eğer select2 daha önce init edilmediyse init et
    if (!window.filterSelectInitialized) {

        $('#filterStatus').select2({
            placeholder: "Select Status",
            width: "100%"
        });

        $('#filterPriority').select2({
            placeholder: "Select Priorities",
            width: "100%"
        });

        $('#filterWorkflow').select2({
            placeholder: "Select Workflow",
            width: "100%"
        });

        window.filterSelectInitialized = true;
    }

    // 2) Önce içleri boşalt
    $('#filterStatus').empty();
    $('#filterPriority').empty();
    $('#filterWorkflow').empty();

    // 3) API'den yükle
    await loadFilterStatus();
    await loadFilterPriorities();
    await loadFilterWorkflows();

    // 4) Refresh (ZORUNLU)
    $('#filterStatus').trigger('change.select2');
    $('#filterPriority').trigger('change.select2');
    $('#filterWorkflow').trigger('change.select2');
}


$(document).on("click", "#btnApplyFilters", function () {

    defaultFilter.statusIds = $('#filterStatus').val();
    defaultFilter.priorityIds = $('#filterPriority').val();
    defaultFilter.workflowIds = $('#filterWorkflow').val();

    const dateFrom = $('#filterDateFrom').val();
    const dateTo = $('#filterDateTo').val();

    defaultFilter.startDateFrom = dateFrom ? moment(dateFrom, "DD.MM.YYYY").toISOString() : null;
    defaultFilter.startDateTo = dateTo ? moment(dateTo, "DD.MM.YYYY").toISOString() : null;
    defaultFilter.workflowIds = $('#filterWorkflow').val();
    $(".filter-task")
        .removeClass("btn-label-secondary")
        .addClass("btn-label-info");
    refreshTaskOverview();

    bootstrap.Offcanvas.getInstance(
        document.getElementById("offcanvasTaskOverviewFilters")
    ).hide();
});

$(document).on("click", "#btnClearFilters", function () {

    $('#filterStatus').val(null).trigger('change');
    $('#filterPriority').val(null).trigger('change');
    $('#filterWorkflow').val(null).trigger('change');

    $('#filterDateFrom').val('');
    $('#filterDateTo').val('');
 
    defaultFilter.statusIds = [];
    defaultFilter.priorityIds = [];
    defaultFilter.workflowIds = [];
    defaultFilter.startDateFrom = null;
    defaultFilter.startDateTo = null;
    $(".filter-task")
        .removeClass("btn-label-info")
        .addClass("btn-label-secondary");
    refreshTaskOverview();
});

$(document).on("click", "#btnCancelFilters", function () {
    $(".filter-task")
        .removeClass("btn-label-info")
        .addClass("btn-label-secondary");
});

async function loadFilterStatus() {

    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`;
        const res = await fetch(url);
        const result = await res.json();

        if (!Array.isArray(result)) {
            console.error("Priorities API beklenen formatta değil:", result);
            return;
        }

        const $priority = $('#filterStatus');
        $priority.empty();

        result.forEach(item => {
            $priority.append(`<option value="${item.id}">${item.name}</option>`);
        });

        $priority.trigger("change");

    } catch (err) {
        console.error("Priorities yüklenirken hata:", err);
    }
}


async function loadFilterPriorities() {

    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetPriorities`;
        const res = await fetch(url);
        const result = await res.json();

        if (!Array.isArray(result)) {
            console.error("Priorities API beklenen formatta değil:", result);
            return;
        }

        const $priority = $('#filterPriority');
        $priority.empty();

        result.forEach(item => {
            $priority.append(`<option value="${item.id}">${item.name}</option>`);
        });

        $priority.trigger("change");

    } catch (err) {
        console.error("Priorities yüklenirken hata:", err);
    }
}

async function loadFilterWorkflows() {

    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Workflow/GetWorkflows`;
        const res = await fetch(url);
        const result = await res.json();

        if (!result || !Array.isArray(result.data)) {
            console.error("Workflow API beklenen formatta değil:", result);
            return;
        }

        const workflows = result.data;

        const $workflow = $('#filterWorkflow');
        $workflow.empty();

        workflows.forEach(item => {
            $workflow.append(`<option value="${item.id}">${item.name}</option>`);
        });

        $workflow.trigger("change");

    } catch (err) {
        console.error("Workflows yüklenirken hata:", err);
    }
}
function showToast(message, type = 'success') {
    const toastEl = document.getElementById('appToast');
    if (!toastEl) return; // toast element yoksa çık

    const toastBody = toastEl.querySelector('.toast-body');
    const toastHeader = toastEl.querySelector('#appToastHeader');

    if (toastBody) toastBody.textContent = message;

    if (toastHeader) {
        // Type’a göre header text veya class değiştirilebilir
        toastHeader.textContent = type.charAt(0).toUpperCase() + type.slice(1); // Baş harf büyük
        toastHeader.className = ''; // Önce class temizle
        toastHeader.classList.add('toast-header', `bg-${type}`, 'text-white'); // Örnek: bg-success, bg-warning
    }


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
function getISODate(dateStr) {
    if (!dateStr) return null; // boşsa null döner

    // dd.mm.yyyy formatını parçala
    const parts = dateStr.split(".");
    if (parts.length !== 3) return null;

    const day = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10) - 1; // JS'de ay 0-based
    const year = parseInt(parts[2], 10);

    const date = new Date(year, month, day);

    // Invalid tarih?
    if (isNaN(date.getTime())) return null;

    // ISO'ya çevir
    return date.toISOString();
}
//-------------------------------- CREATE TASK -------------//

$(document).on("click", ".btnCreateTask", async function () {

    console.log("Create Task açılıyor…");

    // 1) Formu temizle
    await resetCreateTaskForm();

    // 3) Canvas elemanını al
    const el = document.getElementById('offcanvasCreateTask');
    if (!el) {
        console.error("Offcanvas bulunamadı: #offcanvasCreateTask");
        return;
    }

    // 4) Var mı kontrol et
    let canvas = bootstrap.Offcanvas.getInstance(el);
    if (!canvas) {
        canvas = new bootstrap.Offcanvas(el);
    }
    console.log("canvas açıldı");
    canvas.show();
});

async function resetCreateTaskForm() {

    const form = document.getElementById('createTask');

    // 1) Input değerlerini temizle
    form.reset();
    // Select2 temizle
    $('#add-task-type').val(null).trigger("change");
    $('#add-task-category').val(null).trigger("change");
    $('#add-task-assignee').val(null).trigger("change");
    $('#add-task-status').val(null).trigger("change");
    $('#add-task-priority').val(null).trigger("change");
    $('#add-task-workflow').val(null).trigger("change");

    s2MeetingAttendees.val(null).trigger("change");
    $('#add-meeting-start-time').val(null).trigger("change");
    $('#add-meeting-end-time').val(null).trigger("change");

    // Alanları gizle
    $(".task-fields").hide();
    $(".meeting-fields").hide();
    // 3) Flatpickr temizle
    if (fpMeetingStart) fpMeetingStart.clear();
    if (fpMeetingEnd) fpMeetingEnd.clear();
    if (fpDueDate) fpDueDate.clear();

    if (fvCreateTask) {
        fvCreateTask.resetForm(false); // ❗ Alanları “untouched” state'e döndürür
    }

    // 5) FormValidation tüm class’larını temizle (asıl fix burada)
    form.querySelectorAll('[data-field]').forEach(el => {
        el.classList.remove("fv-plugins-icon-container");
        el.classList.remove("fv-plugins-message-container");
        el.classList.remove("fv-valid");
        el.classList.remove("fv-invalid");
    });

    // 6) Inputların kendi Bootstrap class’larını kaldır
    form.querySelectorAll(".form-control, .form-select").forEach(el => {
        el.classList.remove("is-valid");
        el.classList.remove("is-invalid");
    });

    // 7) FormValidation mesajlarını tamamen sil
    form.querySelectorAll('.fv-plugins-message-container').forEach(el => {
        el.innerHTML = "";
    });

    await initCreateTaskUI();
}


let createTaskInitialized = false;

async function initCreateTaskUI() {

    // API’den tüm dropdownları doldur
    await loadCreateTaskTypes();
    await loadCreateTaskCategories();
    await loadCreateTaskStatus();
    await loadCreateTaskPriorities();
    await loadCreateTaskUsers();
    await loadCreateTaskWorkflows();
    fillMeetingTimeDropdowns();

}
async function loadCreateTaskTypes() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskTypes`;
    const res = await fetch(url);
    const list = await res.json();


    const $el = $('#add-task-type');
    $el.empty();

    list.forEach(x => {
        $el.append(`<option value="${x.id}">${x.name}</option>`);
    });

    $el.trigger("change");
}
async function loadCreateTaskCategories() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskCategory`;
    const res = await fetch(url);
    const result = await res.json();

    const $el = $('#add-task-category');
    $el.empty();

    result.forEach(x => {
        $el.append(`<option value="${x.id}">${x.name}</option>`);
    });

    $el.trigger("change");
}
async function loadCreateTaskStatus() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`;
    const res = await fetch(url);
    const list = await res.json();

    const $el = $('#add-task-status');
    $el.empty();

    list.forEach(item => {
        $el.append(`<option value="${item.id}">${item.name}</option>`);
    });

    $el.trigger("change");
}
async function loadCreateTaskPriorities() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetPriorities`;
    const res = await fetch(url);
    const list = await res.json();

    const $el = $('#add-task-priority');
    $el.empty();

    list.forEach(item => {
        $el.append(`<option value="${item.id}">${item.name}</option>`);
    });

    $el.trigger("change");
}

async function loadCreateTaskUsers() {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const list = await res.json();

    const $assignee = $('#add-task-assignee');
    const $attendees = $('#add-meeting-attendees');

    $assignee.empty();
    $attendees.empty();
    
    

    list.data.forEach(u => {
        const option = `<option value="${u.id}">${u.fullName}</option>`;
        $assignee.append(option);
        $attendees.append(option);
    });

    // 3) select2 refresh
    $assignee.val(null).trigger("change");
    $attendees.trigger("change");
    
}
async function loadCreateTaskWorkflows() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Workflow/GetWorkflows`;
    const res = await fetch(url);
    const result = await res.json();

    const $el = $('#add-task-workflow');
    $el.empty();

    result.data.forEach(item => {
        $el.append(`<option value="${item.id}">${item.name}</option>`);
    });

    $el.val(null).trigger("change");
}
$(document).on("change", "#add-task-type", function () {
    const type = Number($(this).val());

    if (type === 1) {           // Task
        $(".task-fields").show();
        $(".meeting-fields").hide();
    }
    else if (type === 2) {      // Meeting
        $(".task-fields").hide();
        $(".meeting-fields").show();
    }
});
function generateTimeOptions(start = "00:00", end = "23:45", stepMinutes = 15) {
    const options = [];
    const [startH, startM] = start.split(":").map(Number);
    const [endH, endM] = end.split(":").map(Number);

    let current = new Date();
    current.setHours(startH, startM, 0, 0);

    const endDate = new Date();
    endDate.setHours(endH, endM, 0, 0);

    while (current <= endDate) {
        let hours = current.getHours();
        let minutes = current.getMinutes();
        let ampm = hours >= 12 ? "PM" : "AM";
        let displayH = hours % 12;
        if (displayH === 0) displayH = 12;
        const displayM = minutes.toString().padStart(2, "0");
        const value = `${hours.toString().padStart(2, "0")}:${displayM}`;
        const text = `${displayH}:${displayM} ${ampm}`;
        options.push({ value, text });

        current.setMinutes(current.getMinutes() + stepMinutes);
    }

    return options;
}
function fillMeetingTimeDropdowns() {
    const options = generateTimeOptions("00:00", "23:45", 15);

    const $start = $('#add-meeting-start-time');
    const $end = $('#add-meeting-end-time');

    // Temizle
    $start.empty();
    $end.empty();

    // Options ekle
    options.forEach(opt => {
        $start.append(`<option value="${opt.value}">${opt.text}</option>`);
        $end.append(`<option value="${opt.value}">${opt.text}</option>`);
    });

    // Select2 refresh
    $start.trigger("change");
    $end.trigger("change");
}
$(document).on("change", "#add-meeting-start-time", function () {
    validateMeetingTimes();
});
$(document).on("change", "#add-meeting-end-time", function () {
    validateMeetingTimes();
});

function validateMeetingTimes() {
    const startDate = $("#add-meeting-start-date").val();
    const endDate = $("#add-meeting-end-date").val();

    const startTime = $("#add-meeting-start-time").val();
    const endTime = $("#add-meeting-end-time").val();

    const $start = $("#add-meeting-start-time");
    const $end = $("#add-meeting-end-time");

    // Eğer tarihler yoksa hiç kontrol yapma
    if (!startDate || !endDate) {
        enableAllMeetingTimeOptions();
        return;
    }

    // --- Tarihler FARKLI ise saat kontrolü yapılmaz ---
    if (startDate !== endDate) {
        enableAllMeetingTimeOptions();
        return;
    }

    // --- Tarihler AYNI ise saat validation devreye girer ---
    $("#add-meeting-end-time option").each(function () {
        const val = $(this).val();
        $(this).prop("disabled", (startTime && val < startTime));
    });

    $("#add-meeting-start-time option").each(function () {
        const val = $(this).val();
        $(this).prop("disabled", (endTime && val > endTime));
    });

    // Eğer seçili değer geçersiz olduysa resetle
    if (startTime && endTime && startTime > endTime) {
        $end.val(null).trigger("change");
    }
    if (startTime && endTime && endTime < startTime) {
        $start.val(null).trigger("change");
    }

    // Select2 refresh
    $start.trigger("change.select2");
    $end.trigger("change.select2");
}

// --- Tüm saat seçeneklerini enable eden helper ---
function enableAllMeetingTimeOptions() {
    $("#add-meeting-end-time option").prop("disabled", false);
    $("#add-meeting-start-time option").prop("disabled", false);

    $("#add-meeting-start-time").trigger("change.select2");
    $("#add-meeting-end-time").trigger("change.select2");
}

function initializeCreateTaskValidation() {

    const form = document.getElementById('createTask');
    if (!form) return;

    const fv = FormValidation.formValidation(form, {
        fields: {
            taskType: {
                validators: {
                    notEmpty: { message: 'Task type is required' }
                }
            },
            taskName: {
                validators: {
                    notEmpty: { message: 'Name is required' },
                    stringLength: {
                        min: 3,
                        max: 250,
                        message: 'Name must be between 3 and 250 characters'
                    }
                }
            },
            taskCategory: {
                validators: {
                    notEmpty: { message: 'Category is required' }
                }
            },
            taskDescription: {
                validators: {
                    notEmpty: { message: 'Description is required' }
                }
            },

            // TASK alanları
            taskAssignee: {
                validators: {
                    callback: {
                        message: 'Assignee is required for tasks',
                        callback: () => $('#add-task-type').val() !== '1' ||
                            $('#add-task-assignee').val()?.trim() !== ''
                    }
                }
            },
            taskStatus: {
                validators: {
                    callback: {
                        message: 'Status is required for tasks',
                        callback: () => $('#add-task-type').val() !== '1' ||
                            $('#add-task-status').val()?.trim() !== ''
                    }
                }
            },
            taskPriority: {
                validators: {
                    callback: {
                        message: 'Priority is required for tasks',
                        callback: () => $('#add-task-type').val() !== '1' ||
                            $('#add-task-priority').val()?.trim() !== ''
                    }
                }
            },
            taskDueDate: {
                validators: {
                    callback: {
                        message: 'Due Date is required for tasks',
                        callback: () => $('#add-task-type').val() !== '1' ||
                            $('#add-task-due-date').val()?.trim() !== ''
                    }
                }
            },
            taskEstimatedHour: {
                validators: {
                    callback: {
                        message: 'Estimated hour is required',
                        callback: () => $('#add-task-type').val() !== '1' ||
                            $('#add-task-estimated-hour').val()?.trim() !== ''
                    }
                }
            },

            // MEETING alanları
            meetingAttendees: {
                validators: {
                    callback: {
                        message: 'Attendees required for meeting',
                        callback: () => $('#add-task-type').val() !== '2' ||
                            ($('#add-meeting-attendees').val() || []).length > 0
                    }
                }
            },
            meetingStartDate: {
                validators: {
                    callback: {
                        message: 'Start Date required',
                        callback: () => $('#add-task-type').val() !== '2' ||
                            $('#add-meeting-start-date').val()?.trim() !== ''
                    }
                }
            },
            meetingStartTime: {
                validators: {
                    callback: {
                        message: 'Start Time required',
                        callback: () => $('#add-task-type').val() !== '2' ||
                            $('#add-meeting-start-time').val()?.trim() !== ''
                    }
                }
            },
            meetingEndDate: {
                validators: {
                    callback: {
                        message: 'End Date required',
                        callback: () => $('#add-task-type').val() !== '2' ||
                            $('#add-meeting-end-date').val()?.trim() !== ''
                    }
                }
            },
            meetingEndTime: {
                validators: {
                    callback: {
                        message: 'End Time required',
                        callback: () => $('#add-task-type').val() !== '2' ||
                            $('#add-meeting-end-time').val()?.trim() !== ''
                    }
                }
            }
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                rowSelector: ".form-control-validation"
            }),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });

    // SUBMIT HANDLE
    const btn = form.querySelector('.data-submit-add');

    btn.addEventListener('click', function () {
        fv.validate().then(async function (status) {

            if (status !== 'Valid') {
                console.log("Form invalid");
                return;
            }

            const payload = buildCreateTaskPayload();
            console.log("Gönderilen:", payload);

            await submitCreateTask(payload);
        });
    });
}
function buildCreateTaskPayload() {

    const typeId = $('#add-task-type').val();

    const payload = {
        name: $('#add-task-name').val(),
        typeId: Number(typeId),
        categoryId: Number($('#add-task-category').val()),
        description: $('#add-task-description').val(),
        workflowId: $('#add-task-workflow').val() || "",
        assigneeIds: [],
        statusId: 0,
        priorityId: 0,
        estimatedHour: 0,
        startDateTime: null,
        endDateTime: null,
        isVirtual: false,
        location: null,
        meetingLink: null,
        ownerId: window.getUserId()
    };

    if (typeId === "1") {
        // TASK
        payload.assigneeIds = [$('#add-task-assignee').val()];
        payload.statusId = Number($('#add-task-status').val());
        payload.priorityId = Number($('#add-task-priority').val());
        payload.estimatedHour = Number($('#add-task-estimated-hour').val());
        const today = new Date();
        const dueDate = $('#add-task-due-date').val();
        payload.startDateTime = toIsoLocal(today);
        payload.endDateTime = $("#add-task-due-date").val()
            ? toIsoLocalFromFlatpickr($("#add-task-due-date").val())
            : null;
    }

    if (typeId === "2") {
        // MEETING
        payload.assigneeIds = $('#add-meeting-attendees').val() || [];

        const sd = $('#add-meeting-start-date').val();
        const st = $('#add-meeting-start-time').val();
        payload.startDateTime = combineDateTime(sd, st);

        const ed = $('#add-meeting-end-date').val();
        const et = $('#add-meeting-end-time').val();
        payload.endDateTime = combineDateTime(ed, et);

        payload.isVirtual = false;
        payload.location = '';
        payload.meetingLink = '';
    }

    return payload;
}
function toIsoLocal(dateObj) {
    const pad = num => String(num).padStart(2, "0");

    return (
        dateObj.getFullYear() +
        "-" + pad(dateObj.getMonth() + 1) +
        "-" + pad(dateObj.getDate()) +
        "T" + pad(dateObj.getHours()) +
        ":" + pad(dateObj.getMinutes()) +
        ":" + pad(dateObj.getSeconds())
    );
}


async function submitCreateTask(payload) {

    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/CreateShortTask`;

        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await res.json();

        if (result.data) {
            await resetCreateTaskForm();
            showToast("Task created successfully!", "success");

            await refreshTaskOverview();

            const c = bootstrap.Offcanvas.getInstance('#offcanvasCreateTask');
            if (c) c.hide();

        } else {
            const errMsg = result.errors || "Task creation failed";
            showToast(errMsg, "error");
        }

    } catch (err) {
        console.error(err);
        showToast("API error occurred", "error");
    }
}

function combineDateTime(dateStr, timeStr) {
    if (!dateStr || !timeStr) return null;

    // dateStr → "24.11.2025"
    const [dd, mm, yyyy] = dateStr.split(".").map(Number);

    // timeStr → "14:30"
    const [hh, min] = timeStr.split(":").map(Number);

    // Hiç Date oluşturmadan doğrudan ISO string üret
    const iso = `${yyyy}-${String(mm).padStart(2, "0")}-${String(dd).padStart(2, "0")}T${String(hh).padStart(2, "0")}:${String(min).padStart(2, "0")}:00`;

    return iso; // NOT Date obj → timezone yok
}
function generateObjectId(existingList = [], idField = "id") {
    let id;
    const existingIds = new Set(existingList.map(item => item[idField]));

    do {
        const timestamp = Math.floor(new Date().getTime() / 1000).toString(16);
        id = timestamp + 'xxxxxxxxxxxxxxxx'.replace(/[x]/g, function () {
            return (Math.floor(Math.random() * 16)).toString(16);
        });
    } while (existingIds.has(id));

    return id.toLowerCase();
}
//--------------------------------END CREATE TASK -------------//

// FULL FORM AÇMA-------------------------------------------------//
document.querySelector('.data-full-form').addEventListener('click',async function () {

    const taskTypeVal = $('#add-task-type').val();
    const btnCreate = document.getElementById("btnCreateTask");

    // Mode ayarları (yeni create)
    btnCreate.textContent = "Create Task";
    btnCreate.setAttribute("data-mode", "create");
    btnCreate.removeAttribute("data-edit-id");

    // Eğer task type seçilmemişse → validation error göster ve çık
    if (!taskTypeVal) {
        if (window.fvCreateTask) {
            fvCreateTask.validateField('taskType');
        }
        return;
    }

    // Görev tipine göre başlık
    const typeText = $('#add-task-type').find('option:selected').text() || 'Task';

    document.getElementById('hdrTask').textContent = `New ${typeText}`;
    document.getElementById('pTask').textContent =
        `Add a new ${typeText} to your workspace and assign it to initiatives & groups.`;

    const btn = document.getElementById("btnCreateTask");
    btn.textContent = "Create Task";
    btn.setAttribute("data-mode", "create");
    btn.removeAttribute("data-edit-id");

    //checklistTasks = task?.checklistTasks || [];


    
    ResetChecklistTaskFormFields();


    


    
    // 🔥 artık fonksiyonu kullanıyoruz
    switchToFullForm();

    if (taskTypeVal === '2') {
        await initFullFormUI_Meeting();
      

        // MEETING

    }
    else if (taskTypeVal === '1') {
        initSubTaskUI();
        updateDependenciesTaskFormFields();
        await initFullFormUI();

    }

    syncNormalToFullForm();





    handleTaskTabs(taskTypeVal);
    if (taskTypeVal === '1') toggleForms('taskFormContainer', 'meetingFormContainer');
    if (taskTypeVal === '2') toggleForms('meetingFormContainer', 'taskFormContainer');

    // Offcanvas kapat
    const offcanvasEl = document.getElementById('offcanvasCreateTask');
    const offcanvas = bootstrap.Offcanvas.getInstance(offcanvasEl);
    if (offcanvas) offcanvas.hide();
});
function switchToFullForm() {
    const normal = document.getElementById('normalFormContainer');
    const full = document.getElementById('fullFormContainer');


    if (normal) normal.classList.add('d-none');
    if (full) full.classList.remove('d-none');

    // Normal formdaki dueDate'i full form'a taşıyoruz
    const normalDueVal = document.getElementById("add-task-due-date").value;

    if (normalDueVal && fpFullTaskDue) {
        fpFullTaskDue.setDate(normalDueVal, true);
    }

}

function toggleForms(showId, hideId) {
    document.getElementById(showId).classList.remove('d-none');
    document.getElementById(hideId).classList.add('d-none');
}

/**
 * Görev tipine göre tab'ları ve nav item'ları yönetir
 * @param {string} taskTypeVal - Task type değeri ('1' = Task, '2' = Meeting, vs.)
 */
function handleTaskTabs(taskTypeVal) {
    const tabMap = {
        '1': ['taskDetailForm', 'taskSubTaskForm', 'taskDependenciesForm', 'taskChecklistForm'], // Task
        '2': ['taskDetailForm', 'taskAgendaForm']                           // Meeting
    };

    const allTabs = ['taskDetailForm', 'taskSubTaskForm', 'taskDependenciesForm', 'taskChecklistForm', 'taskAgendaForm'];
    const visibleTabs = tabMap[taskTypeVal] || [];

    // 🔥 1) Tüm tab-pane aktifliklerini tamamen kaldır
    allTabs.forEach(tabId => {
        const pane = document.getElementById(tabId);
        const nav = document.querySelector(`.nav-pills .nav-link[data-bs-target="#${tabId}"]`);

        pane.classList.remove('show', 'active');
        nav.classList.remove('active');
        nav.parentElement.classList.add('d-none');
    });

    // 🔥 2) Sadece visible tabs görünür olsun
    visibleTabs.forEach(tabId => {
        const nav = document.querySelector(`.nav-pills .nav-link[data-bs-target="#${tabId}"]`);
        nav.parentElement.classList.remove('d-none');
    });

    // 🔥 3) İlk tabı zorla aktif yap (HER ZAMAN DETAILS)
    const firstTabId = visibleTabs[0]; // daima taskDetailForm
    if (firstTabId) {
        const firstNav = document.querySelector(`.nav-pills .nav-link[data-bs-target="#${firstTabId}"]`);
        const tabInstance = new bootstrap.Tab(firstNav);
        tabInstance.show();
    }
}


function resetFullFormValidation() {
    // Task form
    document.querySelectorAll("#taskDetail .form-control, #taskDetail .form-select").forEach(el => {
        el.classList.remove("is-valid", "is-invalid", "fv-invalid", "fv-valid");
    });

    // Meeting form
    document.querySelectorAll("#meetingDetail .form-control, #meetingDetail .form-select").forEach(el => {
        el.classList.remove("is-valid", "is-invalid", "fv-invalid", "fv-valid");
    });

    // Hata mesajlarını temizle
    document.querySelectorAll('.fv-plugins-message-container').forEach(el => el.innerHTML = "");
}
function syncNormalToFullForm() {

    resetFullFormValidation();

    const type = $('#add-task-type').val();

    // -------------------------------------------------
    // TASK (type = 1)
    // -------------------------------------------------
    if (type === "1") {

        // Show Task tab
        document.getElementById("taskFormContainer").classList.remove("d-none");
        document.getElementById("meetingFormContainer").classList.add("d-none");

        // Name
        $('#txt-task-name').val($('#add-task-name').val());

        // Description
        $('#txt-task-description').val($('#add-task-description').val());

        // Workflow
        $('#ddl-task-workflow').val($('#add-task-workflow').val()).trigger('change');

        // Category
        $('#ddl-task-category').val($('#add-task-category').val()).trigger('change');

        // Assignee
        $('#ddl-task-assignee').val($('#add-task-assignee').val()).trigger('change');

        // Status
        $('#ddl-task-status').val($('#add-task-status').val()).trigger('change');

        // Priority
        $('#ddl-task-priority').val($('#add-task-priority').val()).trigger('change');

        // Estimated Hour
        $('#txt-task-estimated-hour').val($('#add-task-estimated-hour').val());

        // Due Date (flatpickr)
        const due = $('#add-task-due-date').val();
        if (due && fpFullTaskDue) {
            fpFullTaskDue.setDate(due, true);
        }
    }

    // -------------------------------------------------
    // MEETING (type = 2)
    // -------------------------------------------------
    else if (type === "2") {

        // Show Meeting tab
        document.getElementById("taskFormContainer").classList.add("d-none");
        document.getElementById("meetingFormContainer").classList.remove("d-none");

        syncNormalToFullForm_Meeting();
    }
}

// FULL FORM workflow değiştikçe SubTask alanı da güncellensin
$(document).on('change', '#ddl-task-workflow', function () {

    const workflowText = $('#ddl-task-workflow option:selected').text() || "";

    // SubTask Workflow alanına yaz
    $('#txt-sub-intiatives').val(workflowText);
});
function fillSelect2($el, list, valueKey = "id", textKey = "name") {
    $el.empty();

    list.forEach(item => {
        $el.append(
            `<option value="${item[valueKey]}">${item[textKey]}</option>`
        );
    });

    $el.trigger("change");
}
async function loadFullFormWorkflows() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Workflow/GetWorkflows`;
    const res = await fetch(url);
    const data = await res.json();

    if (!data || !Array.isArray(data.data)) return;

    fillSelect2($('#ddl-task-workflow'), data.data, "id", "name");
}
async function loadFullFormCategories() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskCategory`;
    const res = await fetch(url);
    const data = await res.json();

    if (!Array.isArray(data)) return;

    fillSelect2($('#ddl-task-category'), data, "id", "name");
}
async function loadFullFormAssignees() {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const response = await res.json();

    fillSelect2($('#ddl-task-assignee'), response.data, "id", "fullName");
}
async function loadFullFormStatuses() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`;
    const res = await fetch(url);
    const data = await res.json();

    if (!Array.isArray(data)) return;

    fillSelect2($('#ddl-task-status'), data, "id", "name");
}
async function loadFullFormPriorities() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetPriorities`;
    const res = await fetch(url);
    const data = await res.json();

    if (!Array.isArray(data)) return;

    fillSelect2($('#ddl-task-priority'), data, "id", "name");
}
async function loadFullFormMeetingCategories() {
    await loadFullFormCategories(); // aynı fonksiyon
    $('#ddl-meeting-category').html($('#ddl-task-category').html()).trigger("change");
}
async function loadFullFormMeetingAttendees() {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const response = await res.json();

    fillSelect2($('#ddl-meeting-attendees'), response.data, "id", "fullName");
}
function loadFullFormTimeDropdowns() {
    const times = generateTimeOptions(); // Senin fonksiyonun
    const $start = $('#ddl-meeting-start-time');
    const $end = $('#ddl-meeting-end-time');

    $start.empty();
    $end.empty();

    times.forEach(t => {
        $start.append(`<option value="${t.value}">${t.text}</option>`);
        $end.append(`<option value="${t.value}">${t.text}</option>`);
    });

    $start.trigger("change");
    $end.trigger("change");
}
async function initFullFormUI() {

    // Task bölümünü doldur
    await loadFullFormWorkflows();
    await loadFullFormCategories();
    await loadFullFormAssignees();
    await loadFullFormStatuses();
    await loadFullFormPriorities();
    loadFullFormTimeDropdowns();
    initTaskFullFormValidation();
}
txtTaskEstimated.addEventListener("input", updateSubTaskMax);
txtTaskEstimated.addEventListener("change", updateSubTaskMax);
function updateSubTaskMax() {
    const totalTaskMinutes = parseInt(txtTaskEstimated.value) || 0;

    // Tüm sub task estimatedHour toplamı
    const usedMinutes = subTaskList.reduce((sum, st) => {
        return sum + (parseInt(st.estimatedHour) || 0);
    }, 0);

    const remainingMinutes = totalTaskMinutes - usedMinutes;

    subHourInput.max = remainingMinutes > 0 ? remainingMinutes : 0;
}
//------------------------- SUB TASKS -------------------------//
async function initSubTaskUI() {
    // 1) Select2 init
    $('#ddl-sub-task-assignee').select2({ width: "100%" });
    $('#ddl-sub-task-category').select2({ width: "100%" });
    $('#ddl-sub-task-status').select2({ width: "100%" });
    $('#ddl-sub-task-priority').select2({ width: "100%" });
    syncFullFormToSubForm();
    const fullTaskStart = fpFullTaskStart?.selectedDates[0] || null;
    const fullTaskDue = fpFullTaskDue?.selectedDates[0] || null;

    // 2) Date pickers
    fpSubTaskStart = flatpickr("#dtSubTaskStartDate", {
        dateFormat: "d.m.Y",
        allowInput: true,
        static: true,
        minDate: fullTaskStart || "today",     // Full Task Start alt sınır
        maxDate: fullTaskDue || null,          // Full Task Due üst sınır
        onChange: function (selectedDates) {
            const start = selectedDates[0];

            // SubTask Due bundan küçük olamaz
            fpSubTaskDue.set("minDate", start);

            const dueVal = fpSubTaskDue.selectedDates[0];
            if (dueVal && dueVal < start) fpSubTaskDue.clear();
        }
    });

    fpSubTaskDue = flatpickr("#dtSubTaskDueDate", {
        dateFormat: "d.m.Y",
        allowInput: true,
        static: true,
        minDate: fullTaskStart || "today",   // Subtask due >= full task start
        maxDate: fullTaskDue || null,        // Subtask due <= full task due
        onChange: function (selectedDates) {

            const due = selectedDates[0];

            // Start bundan büyük olamaz
            fpSubTaskStart.set("maxDate", due);

            const startVal = fpSubTaskStart.selectedDates[0];
            if (startVal && startVal > due) fpSubTaskStart.clear();
        }
    });
    // 3) Dropdownları doldur
    updateSubTaskMax();
    await loadSubTaskUsers();
    await loadSubTaskCategories();
    await loadSubTaskStatus();
    await loadSubTaskPriorities();
}
async function loadSubTaskCategories() {
    const res = await fetch(`${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskCategory`);
    const list = await res.json();

    const $ddl = $('#ddl-sub-task-category');
    $ddl.empty();
    list.forEach(c => {
        $ddl.append(`<option value="${c.id}">${c.name}</option>`);
    });

    $ddl.trigger("change");
}
async function loadSubTaskStatus() {
    const res = await fetch(`${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`);
    const list = await res.json();

    const $ddl = $('#ddl-sub-task-status');
    $ddl.empty();
    list.forEach(s => {
        $ddl.append(`<option value="${s.id}">${s.name}</option>`);
    });

    $ddl.trigger("change");
}
async function loadSubTaskPriorities() {
    const res = await fetch(`${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetPriorities`);
    const list = await res.json();

    const $ddl = $('#ddl-sub-task-priority');
    $ddl.empty();
    list.forEach(p => {
        $ddl.append(`<option value="${p.id}">${p.name}</option>`);
    });

    $ddl.trigger("change");
}
$(document).on("click", "#btnCreateSubTask", function () {

    const name = $('#txt-sub-task-name').val();
    const desc = $('#txt-sub-task-description').val();
    const assigneeId = $('#ddl-sub-task-assignee').val();
    const assigneeName = $('#ddl-sub-task-assignee option:selected').text();

    const categoryId = $('#ddl-sub-task-category').val();
    const categoryName = $('#ddl-sub-task-category option:selected').text();

    const statusId = $('#ddl-sub-task-status').val();
    const statusName = $('#ddl-sub-task-status option:selected').text();

    const priorityId = $('#ddl-sub-task-priority').val();
    const priorityName = $('#ddl-sub-task-priority option:selected').text();

    const start = $('#dtSubTaskStartDate').val()
        ? toIsoLocalFromFlatpickr($('#dtSubTaskStartDate').val())
        : null;
    const due = $('#dtSubTaskDueDate').val()
        ? toIsoLocalFromFlatpickr($('#dtSubTaskDueDate').val())
        : null;
    const estimated = $('#txt-sub-task-estimated-hour').val();
    const ownerId = window.getUserId();

    // Validasyon
    if (!name) {
        showToast("Sub-task name is required", "error");
        return;
    }

    const newSubTask = {
        id: generateObjectId(subTaskList),
        name,
        description: desc,
        assignee: [{ id: assigneeId, name: assigneeName }],
        categoryId,
        categoryName,
        statusId,
        statusName,
        priorityId,
        priorityName,
        startDate: start,
        dueDate: due,
        estimatedHour: estimated,
        typeId: 1,
        typeName: "Task",
        ownerId
    };

    subTaskList.push(newSubTask);
    renderSubTaskList();
    updateSubTaskMax();
    resetSubTaskForm();
});
function renderSubTaskList() {
    const subTasksContainer = document.querySelector("#subTasksContainer");
    subTasksContainer.innerHTML = "";
    console.log("Rendering sub-tasks:", subTaskList);
    subTaskList.forEach((task, index) => {
        subTasksContainer.insertAdjacentHTML("beforeend", renderSubTaskCard(task, index));
    });
}
function renderSubTaskCard(subTask, index) {
    const badgeClass = getPriorityBadgeClass(subTask);
    const assigneeName =
        subTask.assignee && subTask.assignee.length > 0
            ? subTask.assignee[0].name
            : "-";
    const mainTaskName = document.getElementById("txt-task-name").value || "-";
    const cardHtml = `
    <div class="col-md-12 col-lg-12 mb-3">
      <div class="border rounded p-2">
        <div class="row align-items-center">
          
          <!-- Sol içerik -->
          <div class="col">
            <div class="d-flex align-items-center gap-2 mb-1">
              <h5 class="card-title mb-0">${subTask.name}</h5>
              <span class="badge ${badgeClass}">${subTask.priorityName || "-"}</span>
            </div>
            <p class="text-muted small mb-1">${mainTaskName}</p>
            <p class="text-muted extra-small mb-0"> <i class="bx bx-user"></i>
              ${assigneeName || "-"} <i class="bx bx-timer"></i> ${subTask.estimatedHour || 0} min
            </p>
          </div>

          <!-- Sağ delete butonu -->
          <div class="col-auto d-flex align-items-center">
            <a href="javascript:;" class="btn btn-icon btnSubTaskDelete"
  data-id="${subTask.id}" >
    <i class="icon-base bx bx-trash icon-md"></i>
</a>
          </div>

        </div>
      </div>
    </div>
    `;
    return cardHtml;
}



$(document).on("click", ".btnSubTaskDelete", function () {
    const id = $(this).data("id");
    subTaskList = subTaskList.filter(x => x.id !== id);
    renderSubTaskList();
    updateSubTaskMax();
});
function resetSubTaskForm() {
    $('#txt-sub-task-name').val("");
    $('#txt-sub-task-description').val("");
    $('#ddl-sub-task-assignee').val(null).trigger("change");
    $('#ddl-sub-task-category').val(null).trigger("change");
    $('#ddl-sub-task-status').val(null).trigger("change");
    $('#ddl-sub-task-priority').val(null).trigger("change");

    // Flatpickr safe reset
    if (typeof fpSubTaskStart !== "undefined" && fpSubTaskStart && fpSubTaskStart.clear) {
        fpSubTaskStart.clear();
    }

    if (typeof fpSubTaskDue !== "undefined" && fpSubTaskDue && fpSubTaskDue.clear) {
        fpSubTaskDue.clear();
    }

    $('#txt-sub-task-estimated-hour').val("");
}


async function loadSubTaskUsers() {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const list = (await res.json()).data;

    const $ddl = $('#ddl-sub-task-assignee');
    $ddl.empty();
    list.forEach(u => {
        $ddl.append(`<option value="${u.id}">${u.fullName}</option>`);
    });
    $ddl.trigger("change");
}
function syncFullFormToSubForm() {
    // Normal → Full Form alan eşitlemeleri buradaydı (zaten mevcut)

    // 🔥 Workflow → SubTask Workflow
    const workflowText = $('#ddl-task-workflow option:selected').text() || '';

    $('#txt-sub-intiatives').val(workflowText);
}

subHourInput.addEventListener("input", function () {
    const val = parseInt(this.value) || 0;
    const maxVal = parseInt(this.max) || 0;

    if (val > maxVal) {
        this.value = maxVal;
    }
});
function getPriorityBadgeClass(subTask) {

    const priorityId = parseInt(subTask.priorityId); // string "2" ise number 2 yap
    const priorityName = subTask.priorityName?.toLowerCase();

    switch (priorityId || priorityName) {
        case 1:
        case "low":
            return "bg-label-primary";
        case 2:
        case "medium":
            return "bg-label-info";
        case 3:
        case "high":
            return "bg-label-warning";
        case 4:
        case "critical":
            return "bg-label-danger";
        default:
            return "bg-label-secondary"; // bilinmeyen durum için
    }
}

//----------------------- DEPENDENCIES -----------------------//
function updateDependenciesTaskFormFields() {

    // --- 1) Sadece typeId = 1 olanlar
    let filteredTasks = taskOverviewList.filter(t => parseInt(t.typeId) == 1);

    // --- 2) Update modundaysa mevcut task’ı çıkar
    if (globalEditTask?.id) {
        filteredTasks = filteredTasks.filter(t => t.id !== globalEditTask.id);
    }

    populateSelect('ddl-dependencies-task', {
        data: filteredTasks,
        placeholder: 'Select task',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true

    });



    populateSelect('ddl-dependencies-type', {
        apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetDependenciesType`,
        placeholder: 'Select status',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true

    });

}

function ResetDependenciesTaskFormFields() {

    dependenciesTasks = [];
    $("#ddl-dependencies-tasks").val(null).trigger("change");
    $("#dependenciesContainer").html(`
<div class="alert alert-warning alert-dismissible fade show" role="alert">
    <h4 class="alert-heading d-flex align-items-center flex-wrap gap-1">
        <span class="alert-icon rounded-circle bg-warning-subtle p-1">
            <i class="icon-base bx bx-error-circle text-warning"></i>
        </span>
        No Dependencies Added
    </h4>

    <p class="mt-2">
        Your task currently has <strong>no dependency items</strong>.  
        You can add dependencies using the form above.
    </p>

    <hr>

    <p class="mb-0">
        Dependencies help define task relationships such as prerequisites  
        and blocks. Adding them ensures your workflow remains consistent.
    </p>

    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
</div>
`);

    $('#ddl-dependencies-type').val(null).trigger('change');
}


function initializeDependenciesTaskValidation() {

    const dependenciesTaskForm = document.getElementById("dependenciesDetail");
    if (!dependenciesTaskForm) return;

    const fvDependenciesTask = FormValidation.formValidation(dependenciesTaskForm, {
        fields: {
            dependenciesTask: {
                validators: {
                    notEmpty: { message: "Task is required" }
                }
            },
            dependenciesType: {
                validators: {
                    notEmpty: { message: "Type is required" }
                }
            },
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: "",
                rowSelector: (field, ele) => ".form-control-validation"
            }),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });
    return fvDependenciesTask;
}

function addDependenciesTask(dependenciesTask) {
    dependenciesTasks.push(dependenciesTask);
    renderAllDependenciesTasks();
}

const dependenciesTasksContainer = document.querySelector("#dependenciesContainer");
function renderAllDependenciesTasks() {
    dependenciesTasksContainer.innerHTML = "";
    console.log("Rendering dependencies:", dependenciesTasks);
    dependenciesTasks.forEach((task, index) => {
        dependenciesTasksContainer.insertAdjacentHTML("beforeend", renderDependenciesTaskCard(task, index));
    });
}

function getStatusBadgeClass(dependenciesTask) {

    const statusId = parseInt(dependenciesTask.statusId); // string "2" ise number 2 yap
    const statusName = dependenciesTask.statusName?.toLowerCase();

    switch (statusId || statusName) {
        case 1:
        case "to do":
            return "bg-label-info";
        case 2:
        case "in progress":
            return "bg-label-primary";
        case 3:
        case "completed":
            return "bg-label-success";
        case 4:
        case "cancelled":
            return "bg-label-danger";
        default:
            return "bg-label-secondary"; // bilinmeyen durum için
    }
}
function renderDependenciesTaskCard(dependenciesTask, index) {

    const badgeClass = getPriorityBadgeClass(dependenciesTask);
    const statusBadgeClass = getStatusBadgeClass(dependenciesTask);

    const cardHtml = `
    <div class="col-md-12 col-lg-12 mb-3">
      <div class="border rounded p-2">
        <div class="row align-items-center">
          
          <!-- Sol içerik -->
          <div class="col">
            <div class="d-flex align-items-center gap-2 mb-1">
              <h5 class="card-title mb-0">${dependenciesTask.dependenciesTaskName}</h5>
              <span class="badge ${statusBadgeClass}">${dependenciesTask.statusName || "-"}</span>
              <span class="badge ${badgeClass}">${dependenciesTask.priorityName || "-"}</span>
            </div>
            <p class="text-muted small mb-1">${dependenciesTask.mainTaskName || "-"}</p>
          </div>

          <!-- Sağ delete butonu -->
          <div class="col-auto d-flex align-items-center">
            <a href="javascript:;" class="btn btn-icon delete-record"
   onclick="deleteDependenciesTask('${dependenciesTask.id}')">
    <i class="icon-base bx bx-trash icon-md"></i>
</a>
          </div>

        </div>
      </div>
    </div>
    `;
    return cardHtml;
}

function deleteDependenciesTask(id) {
    const index = dependenciesTasks.findIndex(st => st.id === id);
    if (index !== -1) {
        dependenciesTasks.splice(index, 1);
        renderAllDependenciesTasks();
    }
}

const btnCreateDependenciesTask = document.getElementById("btnCreateDependenciesTask");
btnCreateDependenciesTask.addEventListener("click", function () {

    fvDependenciesTask.validate().then(function (status) {

        if (status !== "Valid") {
            console.log("Dependency Task formu geçersiz!");
            return;
        }
        const selectedDependencyId = $("#ddl-dependencies-task").val();

        //dropdown'dan seçilen task'ın detaylarını al
        const task = taskOverviewList.find(t => t.id === selectedDependencyId);

        const dependenciesTask = {
            id: generateObjectId(dependenciesTasks),
            taskId: null,
            dependenciesTaskId: selectedDependencyId,
            dependenciesTaskName: $("#ddl-dependencies-task option:selected").text(),
            dependenciesTypeId: $("#ddl-dependencies-type").val(),
            dependenciesTypeName: $("#ddl-dependencies-type option:selected").text(),
            mainTaskName: document.querySelector("#txt-task-name").value,
            priorityId: task?.priorityId ?? 0,
            priorityName: task?.priorityName ?? "-",
            statusId: task?.statusId ?? 0,
            statusName: task?.statusName ?? "-",
        };
        addDependenciesTask(dependenciesTask);

        // Form reset
        //fvSub.reset();
        $("#ddl-dependencies-task").val(null).trigger("change");
        $("#ddl-dependencies-type").val(null).trigger("change");
        fvDependenciesTask.resetForm(true);

    });
});

//-------------------------- Checklist Tasks -------------------------//
function initializeChecklistTaskValidation() {

    const checklistTaskForm = document.getElementById("checklistDetail");
    if (!checklistTaskForm) return;

    const fvChecklistTask = FormValidation.formValidation(checklistTaskForm, {
        fields: {
            txtChecklistName: {
                validators: {
                    notEmpty: {
                        message: 'Name is required'
                    },
                    stringLength: {
                        min: 3,
                        max: 250,
                        message: 'Name must be between 3 and 250 characters'
                    }
                }
            },
            txtChecklistDescription: {
                validators: {
                    notEmpty: {
                        message: 'Description is required'
                    },
                    stringLength: {
                        max: 2000,
                        message: 'Description cannot exceed 2000 characters'
                    }
                }
            },
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: "",
                rowSelector: (field, ele) => ".form-control-validation"
            }),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });
    return fvChecklistTask;
}
function addChecklistTask(checklistTask) {
    checklistTasks.push(checklistTask);
    renderAllChecklistTasks();
}
const checklistTasksContainer = document.querySelector("#checklistContainer");

function renderAllChecklistTasks() {
    checklistTasksContainer.innerHTML = "";
    console.log("Rendering checklist:", checklistTasks);
    checklistTasks.forEach((task, index) => {
        checklistTasksContainer.insertAdjacentHTML("beforeend", renderChecklistTaskCard(task, index));
    });
}

function renderChecklistTaskCard(checklistTask, index) {
    return `
    <div class="col-12 mb-3 checklist-item" data-index="${index}">
        <div class="border rounded p-2 d-flex justify-content-between align-items-start">

            <div class="d-flex gap-2 align-items-center">
                <!-- DRAG HANDLE -->
                <i class="bx bx-grid-vertical card-handle cursor-move" style="font-size:36px;"></i>

                <div>
                    <h5 class="card-title mb-1">${checklistTask.name}</h5>
                    <p class="text-muted small mb-0">${checklistTask.description || "-"}</p>
                </div>
            </div>

            <a href="javascript:;" class="btn btn-icon delete-record"
               onclick="deleteChecklistTask('${checklistTask.id}')">
                <i class="icon-base bx bx-trash icon-md"></i>
            </a>
        </div>
    </div>
    `;
}
function deleteChecklistTask(id) {
    const index = checklistTasks.findIndex(st => st.id === id);
    if (index !== -1) {
        checklistTasks.splice(index, 1);
        renderAllChecklistTasks();
    }
}
/* -----------------------------------
   SORTABLEJS — DRAG & DROP AKTİF
-----------------------------------*/
new Sortable(checklistTasksContainer, {
    animation: 150,
    handle: ".card-handle",
    draggable: ".checklist-item",

    onEnd: function (evt) {
        // array içi sırayı güncelle
        const moved = checklistTasks.splice(evt.oldIndex, 1)[0];
        checklistTasks.splice(evt.newIndex, 0, moved);

        // tekrar render et
        renderAllChecklistTasks();
    }
});
const btnCreateChecklist = document.getElementById("btnCreateChecklist");
btnCreateChecklist.addEventListener("click", function () {

    fvChecklistTask.validate().then(function (status) {

        if (status !== "Valid") {
            console.log("Sub Task formu geçersiz!");
            return;
        }
        const checklistTask = {
            id: generateObjectId(checklistTasks),
            name: $("#txt-checklist-name").val(),
            description: $("#txt-checklist-description").val(),
        };

        addChecklistTask(checklistTask);

        // Form reset
        //fvSub.reset();
        $("#txt-checklist-name").val('');
        $("#txt-checklist-description").val('');
        fvChecklistTask.resetForm(true);

    });
});

function ResetChecklistTaskFormFields() {
    checklistTasks = [];
    $("#txt-checklist-name").val('');
    $("#txt-checklist-description").val('');
    renderAllChecklistTasks();
}


//-------------------------- CREATE TASK BUTTON -------------------------//

let fvTaskFull = null;

function initTaskFullFormValidation() {

    const form = document.getElementById("taskDetail");
    if (!form) return;

    fvTaskFull = FormValidation.formValidation(form, {
        fields: {
            txtTaskName: { validators: { notEmpty: { message: "Name required" } } },
            txtTaskDescription: { validators: { notEmpty: { message: "Description required" } } },
            ddlTaskAssignee: { validators: { notEmpty: { message: "Assignee required" } } },
            ddlTaskCategory: { validators: { notEmpty: { message: "Category required" } } },
            ddlTaskStatus: { validators: { notEmpty: { message: "Status required" } } },
            ddlTaskPriority: { validators: { notEmpty: { message: "Priority required" } } },

            dtTaskStartDate: { validators: { notEmpty: { message: "Start Date required" } } },
            dtTaskDueDate: { validators: { notEmpty: { message: "Due Date required" } } },
            txtTaskEstimatedHour: { validators: { notEmpty: { message: "Estimated hour required" } } }
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({ rowSelector: ".form-control-validation" }),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });
}

let fvMeetingFull = null;

function initMeetingFullFormValidation() {

    const form = document.getElementById("meetingDetail");
    if (!form) return;

    fvMeetingFull = FormValidation.formValidation(form, {
        fields: {
            txtMeetingName: { validators: { notEmpty: { message: "Name required" } } },
            txtMeetingDescription: { validators: { notEmpty: { message: "Description required" } } },
            ddlMeetingAttendees: { validators: { notEmpty: { message: "Attendees required" } } },
            ddlMeetingCategory: { validators: { notEmpty: { message: "Category required" } } },
            txtMeetingStartDate: { validators: { notEmpty: { message: "Start Date required" } } },
            ddlMeetingStartTime: { validators: { notEmpty: { message: "Start Time required" } } },
            txtMeetingEndDate: { validators: { notEmpty: { message: "End Date required" } } },
            ddlMeetingEndTime: { validators: { notEmpty: { message: "End Time required" } } }
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({ rowSelector: ".form-control-validation" }),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });
}

function getOpenFullFormType() {
    if (!$("#taskFormContainer").hasClass("d-none")) return "task";
    if (!$("#meetingFormContainer").hasClass("d-none")) return "meeting";
    return null;
}
document.getElementById("btnCreateTask")?.addEventListener("click", async function () {

    const mode = this.getAttribute("data-mode") || "create";
    const editId = this.getAttribute("data-edit-id");

    const openForm = getOpenFullFormType();

    if (openForm === "task") {
        // --- TASK FULL FORM VALIDATION ---
        const valid = await fvTaskFull.validate();
        if (valid !== "Valid") {
            console.warn("Task Full Form invalid");
            return;
        }
    }
    else if (openForm === "meeting") {
        // --- MEETING FULL FORM VALIDATION ---
        const valid = await fvMeetingFull.validate();
        if (valid !== "Valid") {
            console.warn("Meeting Full Form invalid");
            return;
        }
    }

    if (mode === "edit" && editId) {

        updateTask(editId);
    }
    else {

        createTask();

    }

   // updateTaskDashboardCards();

    ResetChecklistTaskFormFields();
    ResetDependenciesTaskFormFields();
    resetSubTaskForm();


   // resetAgendaForm();
    // 2. Ortak alanlar
    document.getElementById('fullFormContainer').classList.add('d-none');
    document.getElementById('normalFormContainer').classList.remove('d-none');

});

async function createTask() {

    // 1. ID üretimi (senin tarzında)
    const newId = generateObjectId([taskOverviewList, subTaskList]);
    const taskTypeId = getTaskTypeFromTabs();
    const ownerId = window.getUserId();

    // 3. Değişkenler (Task/Meeting'e göre doldurulur)
    let name = null;
    let categoryId = "";
    let description = null;
    let assigneeIds = [];
    let startDateTime = null;
    let endDateTime = null;
    let priorityId = 0;
    let priorityName = "";
    let statusId = 0;
    let estimatedHour = 0;
    let agendaItems = [];
    let location = "";
    let meetingLink = "";
    let isVirtual = false;
    let intiativeName = "";
    let intiativeId = "";

    //dependenciesTasks
    const updatedDependencies = normalizeDependencies(dependenciesTasks, newId);
    const normalizedSubTasks = normalizeSubTasks(subTaskList);

    if (!taskTypeId) { }
    else if (taskTypeId === "1") {

        name = $('#txt-task-name').val();
        categoryId = $('#ddl-task-category').val();
        //categoryName = $('#ddl-task-category option:selected').text();
        description = $('#txt-task-description').val();
        const selectedAssigneeId = $('#ddl-task-assignee').val();
        assigneeIds = [$('#ddl-task-assignee').val()],          
        statusId = $('#ddl-task-status').val();
        //statusName = $('#ddl-task-status option:selected').text();
        priorityId = $('#ddl-task-priority').val();
        //priorityName = $('#ddl-task-priority option:selected').text();
        estimatedHour = parseInt($('#txt-task-estimated-hour').val()) || 0;
        startDateTime = $("#dtTaskStartDate").val()
            ? toIsoLocalFromFlatpickr($("#dtTaskStartDate").val())
            : null;
        endDateTime = $("#dtTaskDueDate").val()
            ? toIsoLocalFromFlatpickr($("#dtTaskDueDate").val())
            : null;
        intiativeId = $('#ddl-task-workflow').val();
        //intiativeName = $('#ddl-task-workflow option:selected').text();



    }
    else {

        name = $('#txt-meeting-name').val();
        categoryId = $('#ddl-meeting-category').val();
        description = $('#txt-meeting-description').val();
        priorityId = 0;
        estimatedHour = 0;
        location = $('#txt-meeting-location').val();
        meetingLink = $('#txt-meeting-link').val();
        isVirtual = $('#checkVirtualMeeting').is(':checked');
        assigneeIds = $('#ddl-meeting-attendees').val();
        const startDate = $("#txt-meeting-start-date").val();
        const startTime = $("#ddl-meeting-start-time").val();
        const endDate = $("#txt-meeting-end-date").val();
        const endTime = $("#ddl-meeting-end-time").val();
        intiativeId = $('#ddl-meeting-workflow').val();
        startDateTime = getCombinedDateTime(startDate, startTime);
        endDateTime = getCombinedDateTime(endDate, endTime);
        agendaItems = convertAgendaItemsToPayload();
    }

    // >>> SON HAL — BACKEND’İN İSTEDİĞİ FORMAT
    const newTask = {
        id: newId,
        name,
        typeId: parseInt(taskTypeId),
        categoryId: parseInt(categoryId),
        description,
        intiativeId,
        assigneeIds,
        statusId,
        priorityId,
        estimatedHour,
        startDateTime,
        endDateTime,
        isVirtual,
        location,
        meetingLink,
        ownerId,
        subTasks: normalizedSubTasks,
        dependenciesTasks: updatedDependencies,
        checklistTasks: checklistTasks,
        agendaItems: agendaItems
    };

    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/CreateFullTask`;

        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(newTask)
        });

        const result = await res.json();

        if (result.data) {
            showToast("Task created successfully!", "success");

            if (parseInt(taskTypeId) === 1) {
                ResetDependenciesTaskFormFields();
                resetSubTaskForm();
                ResetChecklistTaskFormFields();

            }
            
            
            await refreshTaskOverview();
            
            
            //resetAgendaForm();
            document.getElementById('fullFormContainer').classList.add('d-none');
            document.getElementById('normalFormContainer').classList.remove('d-none');

        } else {
            const errMsg = result.errors || "Task creation failed";
            showToast(errMsg, "error");
        }

    } catch (err) {
        console.error(err);
        showToast("API error occurred", "error");
    }






}
function toIsoLocalFromFlatpickr(dateStr) {
    if (!dateStr) return null;

    // Flatpickr → dd.MM.yyyy formatı
    const [day, month, year] = dateStr.split(".").map(Number);

    return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}T00:00:00`;
}
async function updateTask() {
    // 1. ID üretimi (senin tarzında)
    const newId = generateObjectId([taskOverviewList, subTaskList]);
    const taskTypeId = getTaskTypeFromTabs();
    const ownerId = window.getUserId();

    // 3. Değişkenler (Task/Meeting'e göre doldurulur)
    let name = null;
    let categoryId = "";
    let description = null;
    let assigneeIds = [];
    let startDateTime = null;
    let endDateTime = null;
    let priorityId = 0;
    let priorityName = "";
    let statusId = 0;
    let estimatedHour = 0;
    let agendaItems = [];
    let location = "";
    let meetingLink = "";
    let isVirtual = false;
    let intiativeName = "";
    let intiativeId = "";

    //dependenciesTasks
    const updatedDependencies = normalizeDependencies(dependenciesTasks, newId);
    const normalizedSubTasks = normalizeSubTasks(subTaskList);

    if (!taskTypeId) { }
    else if (taskTypeId === "1") {

        name = $('#txt-task-name').val();
        categoryId = $('#ddl-task-category').val();
        //categoryName = $('#ddl-task-category option:selected').text();
        description = $('#txt-task-description').val();
        const selectedAssigneeId = $('#ddl-task-assignee').val();
        assigneeIds = [$('#ddl-task-assignee').val()],
            statusId = $('#ddl-task-status').val();
        //statusName = $('#ddl-task-status option:selected').text();
        priorityId = $('#ddl-task-priority').val();
        //priorityName = $('#ddl-task-priority option:selected').text();
        estimatedHour = parseInt($('#txt-task-estimated-hour').val()) || 0;
        startDateTime = $("#dtTaskStartDate").val()
            ? toIsoLocalFromFlatpickr($("#dtTaskStartDate").val())
            : null;
        endDateTime = $("#dtTaskDueDate").val()
            ? toIsoLocalFromFlatpickr($("#dtTaskDueDate").val())
            : null;
        intiativeId = $('#ddl-task-workflow').val();
        //intiativeName = $('#ddl-task-workflow option:selected').text();



    }
    else {

        name = $('#txt-meeting-name').val();
        categoryId = $('#ddl-meeting-category').val();
        description = $('#txt-meeting-description').val();
        priorityId = 0;
        estimatedHour = 0;
        location = $('#txt-meeting-location').val();
        meetingLink = $('#txt-meeting-link').val();
        isVirtual = $('#checkVirtualMeeting').is(':checked');
        assigneeIds = $('#ddl-meeting-attendees').val();
        const startDate = $("#txt-meeting-start-date").val();
        const startTime = $("#ddl-meeting-start-time").val();
        const endDate = $("#txt-meeting-end-date").val();
        const endTime = $("#ddl-meeting-end-time").val();
        intiativeId = $('#ddl-meeting-workflow').val();
        startDateTime = getCombinedDateTime(startDate, startTime);
        endDateTime = getCombinedDateTime(endDate, endTime);
        agendaItems = convertAgendaItemsToPayload();
    }

    // >>> SON HAL — BACKEND’İN İSTEDİĞİ FORMAT
    const newTask = {
        id: globalEditTask.id,
        name,
        typeId: parseInt(taskTypeId),
        categoryId: parseInt(categoryId),
        description,
        intiativeId,
        assigneeIds,
        statusId,
        priorityId,
        estimatedHour,
        startDateTime,
        endDateTime,
        isVirtual,
        location,
        meetingLink,
        ownerId,
        subTasks: normalizedSubTasks,
        dependenciesTasks: updatedDependencies,
        checklistTasks: checklistTasks,
        agendaItems: agendaItems
    };

    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/UpdateFullTask`;

        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(newTask)
        });

        const result = await res.json();

        if (result.data) {
            showToast("Task updated successfully!", "success");

            if (parseInt(taskTypeId) === 1) {
                ResetDependenciesTaskFormFields();
                resetSubTaskForm();
                ResetChecklistTaskFormFields();

            }
            globalEditTask = null;
            subTaskList = [];
            dependenciesTasks = [];
            checklistTasks = [];
            

            await refreshTaskOverview();


            //resetAgendaForm();
            document.getElementById('fullFormContainer').classList.add('d-none');
            document.getElementById('normalFormContainer').classList.remove('d-none');

        } else {
            const errMsg = result.errors || "Task creation failed";
            showToast(errMsg, "error");
        }

    } catch (err) {
        console.error(err);
        showToast("API error occurred", "error");
    }

}

document.getElementById('btnBackToNormal').addEventListener('click', function () {
    document.getElementById('fullFormContainer').classList.add('d-none');
    document.getElementById('normalFormContainer').classList.remove('d-none');
});
document.getElementById('btnCancelTask').addEventListener('click', function () {
    document.getElementById('fullFormContainer').classList.add('d-none');
    document.getElementById('normalFormContainer').classList.remove('d-none');
});

//-------------------------- EDIT TASK -------------------------//
function getTaskTypeFromTabs() {
    // Task tabs
    const taskTabs = [
        "taskSubTaskForm",
        "taskDependenciesForm",
        "taskChecklistForm"
    ];

    // Meeting tab
    const meetingTabs = [
        "taskAgendaForm"
    ];

    // TASK MODU AÇIK MI?
    const anyTaskTabVisible = taskTabs.some(tabId => {
        const navItem = document.querySelector(
            `.nav-pills .nav-link[data-bs-target="#${tabId}"]`
        )?.parentElement;

        return navItem && !navItem.classList.contains("d-none");
    });

    if (anyTaskTabVisible) return "1"; // TASK

    // MEETING MODU AÇIK MI?
    const anyMeetingTabVisible = meetingTabs.some(tabId => {
        const navItem = document.querySelector(
            `.nav-pills .nav-link[data-bs-target="#${tabId}"]`
        )?.parentElement;

        return navItem && !navItem.classList.contains("d-none");
    });

    if (anyMeetingTabVisible) return "2"; // MEETING

    return null; // Belirlenemedi (olmaz ama güvenlik için)
}

function toISO(dateStr) {
    if (!dateStr) return null;

    // Eğer YYYY-MM-DD gelirse direkt ISO
    if (dateStr.includes("T")) return dateStr;

    // DD.MM.YYYY format → ISO
    const parts = dateStr.split(".");
    if (parts.length === 3) {
        const [dd, mm, yyyy] = parts;
        return new Date(`${yyyy}-${mm}-${dd}T00:00:00Z`).toISOString();
    }

    return new Date(dateStr).toISOString();
}
function normalizeSubTasks(list) {
    return list.map(st => ({
        id: st.id,
        name: st.name,
        description: st.description,
        assignee: st.assignee,
        categoryId: parseInt(st.categoryId),
        categoryName: st.categoryName,
        statusId: parseInt(st.statusId),
        statusName: st.statusName,
        priorityId: parseInt(st.priorityId),
        priorityName: st.priorityName,
        estimatedHour: parseInt(st.estimatedHour),
        startDate: toISO(st.startDate),
        dueDate: toISO(st.dueDate),
        typeId: st.typeId,
        typeName: st.typeName,
        ownerId: st.ownerId
    }));
}
function normalizeDependencies(list, parentId) {
    return list.map(dep => ({
        ...dep,
        taskId: parentId,
        dependenciesTypeId: parseInt(dep.dependenciesTypeId),
        priorityId: parseInt(dep.priorityId),
        statusId: parseInt(dep.statusId)
    }));
}

//-------------------------- MEETING -------------------------//

function syncNormalToFullForm_Meeting() {

    $('#checkVirtualMeeting').prop('checked', false);
    $('#txt-meeting-location').val('');
    $('#txt-meeting-link').val('');

    // Checkbox duruma göre enable/disable ayarı
    if (!$('#checkVirtualMeeting').prop('checked')) {
        $('#txt-meeting-location').prop('disabled', false); // Location enable
        $('#txt-meeting-link').prop('disabled', true);      // Link disable
    }
    

    $("#txt-meeting-name").val($("#add-task-name").val());
    $("#txt-meeting-description").val($("#add-task-description").val());

    // Workflow
    const wf = $("#add-task-workflow").val();
    $("#ddl-meeting-workflow").val(wf).trigger("change");

    // Attendees
    const attendees = $("#add-meeting-attendees").val() || [];
    $("#ddl-meeting-attendees").val(attendees).trigger("change");

    // Start date + time
    $("#txt-meeting-start-date").val($("#add-meeting-start-date").val());
    $("#ddl-meeting-start-time").val($("#add-meeting-start-time").val()).trigger("change");

    // End date + time
    $("#txt-meeting-end-date").val($("#add-meeting-end-date").val());
    $("#ddl-meeting-end-time").val($("#add-meeting-end-time").val()).trigger("change");

    // Virtual + Link + Location
    const isVirtual = $("#checkVirtualMeeting").is(":checked");
    $("#checkVirtualMeeting").prop("checked", isVirtual);

    if (isVirtual) {
        $("#txt-meeting-link").prop("disabled", false).val($("#txt-meeting-link").val());
        $("#txt-meeting-location").prop("disabled", true).val("");
    } else {
        $("#txt-meeting-location").prop("disabled", false).val($("#txt-meeting-location").val());
        $("#txt-meeting-link").prop("disabled", true).val("");
    }
}



let fpFullMeetingStart = null;
let fpFullMeetingEnd = null;

async function initFullFormUI_Meeting() {

    // Select2 init
    $('#ddl-meeting-attendees').select2({ width: "100%" });
    $('#ddl-meeting-category').select2({ width: "100%" });
    $('#ddl-meeting-start-time').select2({ width: "100%" });
    $('#ddl-meeting-end-time').select2({ width: "100%" });
    $('#ddl-meeting-workflow').select2({ width: "100%" });

    // 1) Attendees yükle
    await loadMeetingAttendeesFullForm();

    // 2) Category yükle
    await loadMeetingCategoriesFullForm();

    // 3) Workflow yükle
    await loadMeetingWorkflowsFullForm();

    // 4) Time options
    fillMeetingTimeDropdownsFull();

    // 5) Date pickers
    fpFullMeetingStart = flatpickr("#txt-meeting-start-date", {
        dateFormat: "d.m.Y",
        allowInput: true,
        static: true,
        onChange: function (selectedDates) {
            const start = selectedDates[0];
            fpFullMeetingEnd.set("minDate", start);

            const endVal = fpFullMeetingEnd.selectedDates[0];
            if (endVal && endVal < start) fpFullMeetingEnd.clear();

            validateMeetingTimesFull();
            agendaNeedRefresh = true;
        }
    });
    fpFullMeetingEnd = flatpickr("#txt-meeting-end-date", {
        dateFormat: "d.m.Y",
        allowInput: true,
        static: true,
        onChange: function (selectedDates) {
            const end = selectedDates[0];
            fpFullMeetingStart.set("maxDate", end);

            const startVal = fpFullMeetingStart.selectedDates[0];
            if (startVal && startVal > end) fpFullMeetingStart.clear();

            validateMeetingTimesFull();
            agendaNeedRefresh = true;
        }
    });
    // 6) Saat değişiminde validate
    $(document).on("change", "#ddl-meeting-start-time", function () {
        validateMeetingTimesFull();
        agendaNeedRefresh = true;
    });
    $(document).on("change", "#ddl-meeting-end-time", function () {
        validateMeetingTimesFull();
        agendaNeedRefresh = true;
    });
    // Virtual meeting checkbox behavior
    $("#checkVirtualMeeting").on("change", function () {
        if ($(this).is(":checked")) {
            $("#txt-meeting-link").prop("disabled", false);
            $("#txt-meeting-location").prop("disabled", true).val("");
        } else {
            $("#txt-meeting-link").prop("disabled", true).val("");
            $("#txt-meeting-location").prop("disabled", false);
        }
    });

    initMeetingFullFormValidation();


}
async function loadMeetingAttendeesFullForm() {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const json = await res.json();

    const $att = $("#ddl-meeting-attendees");
    $att.empty();

    json.data.forEach(u => {
        $att.append(`<option value="${u.id}">${u.fullName}</option>`);
    });

    $att.trigger("change");
}
async function loadMeetingCategoriesFullForm() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskCategory`;
    const res = await fetch(url);
    const json = await res.json();

    const $cat = $("#ddl-meeting-category");
    $cat.empty();

    json.forEach(c => {
        $cat.append(`<option value="${c.id}">${c.name}</option>`);
    });

    $cat.trigger("change");
}
async function loadMeetingWorkflowsFullForm() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Workflow/GetWorkflows`;
    const res = await fetch(url);
    const json = await res.json();

    const workflows = json.data;

    const $wf = $("#ddl-meeting-workflow");
    $wf.empty();

    // ⭐ 1) Boş placeholder ekle (allowClear çalışması için zorunlu)
    $wf.append(`<option value=""></option>`);

    workflows.forEach(w => {
        $wf.append(`<option value="${w.id}">${w.name}</option>`);
    });

    // ⭐ 3) Select2 initialize (dropdownParent varsa ekleyebilirsin)
    $wf.select2({
        placeholder: "Select workflow...",
        allowClear: true
       
    });

    // Varsayılan bir seçim yapma
    $wf.val(null).trigger("change");
}
function fillMeetingTimeOptionsFullForm() {
    const options = generateTimeOptions();

    const $start = $("#ddl-meeting-start-time");
    const $end = $("#ddl-meeting-end-time");

    $start.empty();
    $end.empty();

    options.forEach(t => {
        $start.append(`<option value="${t.value}">${t.text}</option>`);
        $end.append(`<option value="${t.value}">${t.text}</option>`);
    });

    $start.trigger("change");
    $end.trigger("change");
}
function getTodayDMY() {
    const d = new Date();
    return `${d.getDate().toString().padStart(2, '0')}.${(d.getMonth() + 1)
        .toString().padStart(2, '0')}.${d.getFullYear()}`;
}
function fillMeetingTimeDropdownsFull() {

    const options = generateTimeOptions("00:00", "23:45", 15);

    const $start = $('#ddl-meeting-start-time');
    const $end = $('#ddl-meeting-end-time');

    $start.empty();
    $end.empty();

    options.forEach(opt => {
        $start.append(`<option value="${opt.value}">${opt.text}</option>`);
        $end.append(`<option value="${opt.value}">${opt.text}</option>`);
    });

    $start.trigger("change");
    $end.trigger("change");
}
function validateMeetingTimesFull() {

    const startDate = $("#txt-meeting-start-date").val();
    const endDate = $("#txt-meeting-end-date").val();

    const startTime = $("#ddl-meeting-start-time").val();
    const endTime = $("#ddl-meeting-end-time").val();

    const $start = $("#ddl-meeting-start-time");
    const $end = $("#ddl-meeting-end-time");

    // Tarih yok → tüm seçenekler enable
    if (!startDate || !endDate) {
        enableFullMeetingTimeOptions();
        return;
    }

    // Tarihler farklı → saat kısıtı yok
    if (startDate !== endDate) {
        enableFullMeetingTimeOptions();
        return;
    }

    // Tarihler aynı → saat kısıtı devrede
    $("#ddl-meeting-end-time option").each(function () {
        const val = $(this).val();
        $(this).prop("disabled", (startTime && val < startTime));
    });

    $("#ddl-meeting-start-time option").each(function () {
        const val = $(this).val();
        $(this).prop("disabled", (endTime && val > endTime));
    });

    if (startTime && endTime && startTime > endTime) {
        $end.val(null).trigger("change");
    }

    $start.trigger("change.select2");
    $end.trigger("change.select2");
}

function enableFullMeetingTimeOptions() {
    $("#ddl-meeting-end-time option").prop("disabled", false);
    $("#ddl-meeting-start-time option").prop("disabled", false);
}

async function initAgendaDefaultItem() {
    const container = document.getElementById("agendaDetailView");
    container.innerHTML = ""; // Temizle

    const template = document.getElementById("agenda-item-template");
    const newItem = template.content.cloneNode(true);

    // --- Meeting Values ---
    const meetingName = $("#txt-meeting-name").val();
    const meetingStartDate = $("#txt-meeting-start-date").val();
    const meetingEndDate = $("#txt-meeting-end-date").val();
    const meetingStartTime = $("#ddl-meeting-start-time").val();
    const meetingEndTime = $("#ddl-meeting-end-time").val();
    const selectedAttendees = $("#ddl-meeting-attendees").val();

    // --- Fill default item ---
    newItem.querySelector(".agenda-title").value = meetingName || "";

    const $agendaAttendees = $(newItem.querySelector(".agenda-attendees"));
    // meeting attendees dropdown'ın doldurulmasını bekle
    await fillAgendaAttendees($agendaAttendees);

    // select2 init
    $agendaAttendees.select2({
        width: "100%",
        placeholder: "Select attendees"
    });
    // Seçili değerleri ata
    $agendaAttendees.val(selectedAttendees).trigger("change");
    // date
    newItem.querySelector(".agenda-start-date").value = meetingStartDate;
    newItem.querySelector(".agenda-end-date").value = meetingEndDate;

    // time
    const $startT = $(newItem.querySelector(".agenda-start-time"));
    const $endT = $(newItem.querySelector(".agenda-end-time"));

    fillAgendaTimeDropdown($startT);
    fillAgendaTimeDropdown($endT);

    // select2 init
    $startT.select2({ width: "100%" });
    $endT.select2({ width: "100%" });



    $startT.val(meetingStartTime).trigger("change");
    $endT.val(meetingEndTime).trigger("change");

    container.appendChild(newItem);

    renumberAgendaItems();
    bindAgendaFlatpickr();
}
function fillAgendaTimeDropdown($select) {
    const options = generateTimeOptions("00:00", "23:45", 15);
    $select.empty();
    options.forEach(o => {
        $select.append(`<option value="${o.value}">${o.text}</option>`);
    });
    $select.trigger("change");
}

function addAgendaItem() {
    
    const container = document.getElementById("agendaDetailView");
    const items = container.querySelectorAll(".agenda-item");

    // ❗ Hiç item yoksa Add Item çalışmamalı
    //if (items.length === 0) {
    //    console.warn("No agenda item exists — default item must be created first. Ignoring Add Item.");
    //    return;
    //}

    const template = document.getElementById("agenda-item-template");
    const newItem = template.content.cloneNode(true);

    // ----- Previous item -----
    const prevItem = items[items.length - 1];

    const prevEndDate = prevItem.querySelector(".agenda-end-date").value;
    const prevEndTime = $(prevItem.querySelector(".agenda-end-time")).val();

    const meetingEndDate = $("#txt-meeting-end-date").val();
    const meetingEndTime = $("#ddl-meeting-end-time").val();

    // Attendees
    const $attendees = $(newItem.querySelector(".agenda-attendees"));
    fillAgendaAttendees($attendees);
    $attendees.select2({ width: "100%" });

    // Time dropdowns
    const $startT = $(newItem.querySelector(".agenda-start-time"));
    const $endT = $(newItem.querySelector(".agenda-end-time"));

    fillAgendaTimeDropdown($startT);
    fillAgendaTimeDropdown($endT);

    $startT.select2({ width: "100%" });
    $endT.select2({ width: "100%" });

    // Default values
    newItem.querySelector(".agenda-start-date").value = prevEndDate;
    newItem.querySelector(".agenda-end-date").value = meetingEndDate;

    $startT.val(prevEndTime).trigger("change");
    $endT.val(meetingEndTime).trigger("change");

    container.appendChild(newItem);

    renumberAgendaItems();
    bindAgendaFlatpickr();
}
function fillAgendaAttendees($select) {
    const list = $("#ddl-meeting-attendees option");

    $select.empty();
    list.each(function () {
        $select.append(`<option value="${$(this).val()}">${$(this).text()}</option>`);
    });

    $select.trigger("change");
}

function bindAgendaFlatpickr() {
    $("#agendaDetailView .agenda-item").each(function () {

        const startInput = this.querySelector(".agenda-start-date");
        const endInput = this.querySelector(".agenda-end-date");

        // Eğer flatpickr zaten attach edilmişse tekrar oluşturma
        if (!startInput._fp) {
            startInput._fp = flatpickr(startInput, {
                dateFormat: "d.m.Y",
                allowInput: true,
                static: true,
                minDate: $("#txt-meeting-start-date").val(),
                maxDate: $("#txt-meeting-end-date").val()
            });
        }

        if (!endInput._fp) {
            endInput._fp = flatpickr(endInput, {
                dateFormat: "d.m.Y",
                allowInput: true,
                static: true,
                minDate: $("#txt-meeting-start-date").val(),
                maxDate: $("#txt-meeting-end-date").val()
            });
        }
    });
}


function renumberAgendaItems() {
    $("#agendaDetailView .agenda-item").each(function (i) {
        $(this).find(".agenda-number").text(i + 1);
    });
}

$(document).on("click", "#btnCreateAgenda", function () {
    addAgendaItem();
});
function convertAgendaItemsToPayload() {

    const items = [];
    const rows = document.querySelectorAll("#agendaDetailView .agenda-item:not(.d-none)");

    rows.forEach((row, index) => {
        const startDate = row.querySelector(".agenda-start-date").value;
        const startTime = row.querySelector(".agenda-start-time").value;
        const endDate = row.querySelector(".agenda-end-date").value;
        const endTime = row.querySelector(".agenda-end-time").value;
        const agendaTitle = row.querySelector(".agenda-title").value;
        const attendees = $(row).find(".agenda-attendees").val() || [];

        items.push({
            orderNo: index + 1,
            startDate: getCombinedDateTime(startDate, startTime),
            endDate: getCombinedDateTime(endDate, endTime),
            name: agendaTitle,
            attendees: attendees.map(id => ({
                id: id,
                name: $('#ddl-meeting-attendees option[value="' + id + '"]').text()
            }))
        });
    });





    //rows.each(function () {

    //    const title = $(this).find(".agenda-title").val() || "";
    //    const attendees = $(this).find(".agenda-attendees").val() || [];
    //    const startTime = $(this).find(".agenda-start-time").val();
    //    const endTime = $(this).find(".agenda-end-time").val();

    //    const startInput = this.querySelector(".agenda-start-date");
    //    const endInput = this.querySelector(".agenda-end-date");

    //    // Flatpickr instance üzerinden date alıyoruz
    //    const startDate = startInput._fp?.selectedDates[0] || null;
    //    const endDate = endInput._fp?.selectedDates[0] || null;

    //    const startDateTime = getCombinedDateTime(
    //        formatDateForCombined(startDate),
    //        startTime
    //    );

    //    const endDateTime = getCombinedDateTime(
    //        formatDateForCombined(endDate),
    //        endTime
    //    );




    //    items.push({
    //        id: generateObjectId([agendaList]),
    //        name: title,
    //        attendees: attendees.map(a => ({
    //            id: a,
    //            name: $("#ddl-meeting-attendees option[value='" + a + "']").text()
    //        })),
    //        startDate: startDateTime,
    //        endDate: endDateTime
    //    });
    //});

    return items;
}
function formatDateForCombined(dateObj) {
    if (!dateObj) return null;
    return dateObj.toLocaleDateString("tr-TR"); // "24.11.2025"
}
// MEETING NAME değişirse → Agenda reset
$(document).on("input", "#txt-meeting-name", function () {
    agendaNeedRefresh = true;
});
$(document).on("change", "#ddl-meeting-attendees", function () {
agendaNeedRefresh = true;});
function resetAgendaAndInitDefault() {
    // Agenda reset
    document.getElementById("agendaDetailView").innerHTML = "";

    // Default agenda item oluştur
    initAgendaDefaultItem();
}
let agendaTabLoaded = false;
let agendaNeedRefresh = false;

$('[data-bs-target="#taskAgendaForm"]').on("shown.bs.tab", function () {
    console.log("Agenda tab activated");

    // İlk açılış
    if (!agendaTabLoaded) {

        // ---- EDIT MODE ----
        if (window.globalEditTask) {

            const agendas = window.globalEditTask.agendaItems || [];

            if (agendas.length > 0) {
                console.log("Rendering agenda items from task...");
                renderAgendaFromTask(agendas);     // ← Task’taki itemları doldur
            } else {
                console.log("Task has no agenda → creating default item");
                initAgendaDefaultItem();           // ← Task boşsa default
            }
        }
        // ---- CREATE MODE ----
        else {
            console.log("Create mode → default agenda");
            initAgendaDefaultItem();
        }

        agendaTabLoaded = true;
        agendaNeedRefresh = false;
        return;
    }


    // İlk açılış değil → ama değişiklik yapılmış
    if (agendaNeedRefresh) {
        console.log("Agenda refresh needed → resetting");
        resetAgendaAndInitDefault();
        agendaNeedRefresh = false;
    }
});

function renderAgendaFromTask(agendaItems) {

    const container = document.getElementById("agendaDetailView");
    container.innerHTML = "";

    agendaItems.sort((a, b) => a.orderNo - b.orderNo);

    agendaItems.forEach(item => {
        const template = document.getElementById("agenda-item-template");
        const node = template.content.cloneNode(true);

        // Title
        node.querySelector(".agenda-title").value = item.name;

        // Attendees
        const $attendees = $(node.querySelector(".agenda-attendees"));
        fillAgendaAttendees($attendees).then(() => {
            $attendees.val(item.attendees.map(a => a.id)).trigger("change");
        });
        $attendees.select2({ width: "100%" });

        // Dates
        node.querySelector(".agenda-start-date").value =
            formatDateForFlatpickr(item.startDate);   // 25.11.2025

        node.querySelector(".agenda-end-date").value =
            formatDateForFlatpickr(item.endDate);

        // Times
        const $startT = $(node.querySelector(".agenda-start-time"));
        const $endT = $(node.querySelector(".agenda-end-time"));

        fillAgendaTimeDropdown($startT);
        fillAgendaTimeDropdown($endT);

        const { time: startTime } = splitDateTime(item.startDate);
        const { time: endTime } = splitDateTime(item.endDate);

        $startT.select2({ width: "100%" });
        $endT.select2({ width: "100%" });

        $startT.val(startTime).trigger("change");
        $endT.val(endTime).trigger("change");

        container.appendChild(node);
    });

    renumberAgendaItems();
    bindAgendaFlatpickr();
}





$(document).on("click", ".btn-remove-agenda", function (e) {
    e.preventDefault();
    removeAgendaItem(this);
});
function removeAgendaItem(btn) {
    const container = document.getElementById("agendaDetailView");
    const items = container.querySelectorAll(".agenda-item");

    if (items.length <= 1) {
        showToast("At least one agenda item is required.", "warning");
        return;
    }

    const item = btn.closest(".agenda-item");
    if (!item) return;

    const index = Array.from(items).indexOf(item);

    // ❗ Silinecek item’ın zaman bilgisi
    const prevItem = index > 0 ? items[index - 1] : null;
    const nextItem = index < items.length - 1 ? items[index + 1] : null;

    // ❗ Bağlantı: silinen item’ın SONRAKİ item'ı üzerinde işlem yapacağız
    if (prevItem && nextItem) {
        const prevEndDate = prevItem.querySelector(".agenda-end-date").value;
        const prevEndTime = $(prevItem.querySelector(".agenda-end-time")).val();

        // NEXT item → Start = prevItem.End
        nextItem.querySelector(".agenda-start-date").value = prevEndDate;

        const $nextStartTime = $(nextItem.querySelector(".agenda-start-time"));
        $nextStartTime.val(prevEndTime).trigger("change");
    }

    // ❗ Itemı kaldır
    item.remove();

    // Yeniden numaralandır
    renumberAgendaItems();

    // Flatpickr için yeniden init
    bindAgendaFlatpickr();
}

function getCombinedDateTime(dateStr, timeStr) {
    if (!dateStr || !timeStr) return null;

    const [day, month, year] = dateStr.split(".").map(Number);
    const [hour, minute] = timeStr.split(":").map(Number);

    // YYYY-MM-DDTHH:mm:00 şeklinde local time stringi üret
    const isoLocal = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00`;

    return isoLocal;
}

//-------------------------- EDIT FULL FORM TASK CREATION -------------------------//
let globalEditTask = null;
$(document).on('click', '.edit-task',async function () {
    const id = $(this).data('id'); // satır ID'si

    // 🔥 Açık responsive modal varsa kapat
    const modalEl = document.querySelector('.dtr-bs-modal.show');
    if (modalEl) {
        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();
    }

    const task = taskOverviewList.find(m => m.id === id);
    globalEditTask = task;
    const taskTypeText = task?.typeName || 'Task';

    const btn = document.getElementById("btnCreateTask");
    btn.textContent = `Update ${taskTypeText}`;
    btn.setAttribute("data-mode", "edit");
    btn.setAttribute("data-edit-id", task.id);

    document.getElementById('hdrTask').textContent = `Edit ${taskTypeText} / ${task.name}`;
    document.getElementById('pTask').textContent =
        `Edit a  ${taskTypeText} to your workspace and assign it to a workflows and group`;

    handleTaskTabs(String(task.typeId));
    if (String(task.typeId) === '1') toggleForms('taskFormContainer', 'meetingFormContainer');
    if (String(task.typeId) === '2') toggleForms('meetingFormContainer', 'taskFormContainer');

    if (parseInt(task.typeId) === 1) {

        await initFullFormUI();
        await syncNormalToFullEdit();

    } else if (parseInt(task.typeId) === 2) {


        await initFullFormUI_Meeting();

        syncNormalToFullForm_EditMeeting();
    }






    document.getElementById('normalFormContainer').classList.add('d-none');
    document.getElementById('fullFormContainer').classList.remove('d-none');
});

function syncNormalToFullForm_EditMeeting() {

    $("#txt-meeting-name").val(globalEditTask.name);
    $("#txt-meeting-description").val(globalEditTask.description);

    const wf = globalEditTask?.workflowId ?? "";
    $("#ddl-meeting-workflow").val(wf).trigger("change");

    $('#checkVirtualMeeting').prop('checked', globalEditTask.isVirtual === true);
    $('#txt-meeting-location').val(globalEditTask.location || '');
    $('#txt-meeting-link').val(globalEditTask.meetingLink || '');
    // Checkbox duruma göre enable/disable ayarı
    // toplantı virtual ise location disable, link enable
    if (globalEditTask.isVirtual === true) {
        $('#txt-meeting-location').prop('disabled', true);
        $('#txt-meeting-link').prop('disabled', false);
    } else {
        $('#txt-meeting-location').prop('disabled', false);
        $('#txt-meeting-link').prop('disabled', true);
    }
    const mainStart = document.querySelector("#ddl-meeting-start-time");
    const mainEnd = document.querySelector("#ddl-meeting-end-time");
    const { dateDisplay: startDate, time: startTime } = splitDateTimeLocal(globalEditTask.startDate);
    const { dateDisplay: endDate, time: endTime } = splitDateTimeLocal(globalEditTask.endDate);

    document.getElementById("txt-meeting-start-date")._flatpickr.setDate(startDate, true);

    document.getElementById("txt-meeting-end-date")._flatpickr.setDate(endDate, true);
    const fixedStartTime = hasOption(mainStart, startTime)
        ? startTime
        : roundTimeToOptions(startTime);
    const fixedEndTime = hasOption(mainEnd, endTime)
        ? endTime
        : roundTimeToOptions(endTime);

        
    // 🟢 Saatleri select'e set et
    $("#ddl-meeting-start-time").val(fixedStartTime).trigger("change");
    $("#ddl-meeting-end-time").val(fixedEndTime).trigger("change");

    // 1️⃣ Attendees id listesi
    const attendeeIds = (globalEditTask?.assignee || []).map(a => a.id);

    // 2️⃣ Eğer attendees listesi boşsa hiçbir şey seçme
    if (!attendeeIds.length) {
        $('#ddl-meeting-attendees').val([]).trigger("change");
        return;
    }
    $('#ddl-meeting-attendees').val(attendeeIds).trigger("change");

}
function roundTimeToOptions(timeStr) {
    if (!timeStr) return null;

    let [h, m] = timeStr.split(":").map(Number);

    // 15 dakikaya yuvarla (aşağı)
    const roundedMinutes = Math.floor(m / 15) * 15;

    const hh = String(h).padStart(2, "0");
    const mm = String(roundedMinutes).padStart(2, "0");

    return `${hh}:${mm}`; // ör: "09:30"
}

function splitDateTimeLocal(iso) {
    if (!iso) return { dateIso: "", dateDisplay: "", time: "" };

    // iso → "2025-12-02T01:45:00"
    const [datePart, timePart] = iso.split("T");

    const [year, month, day] = datePart.split("-");
    const [hour, minute] = timePart.split(":");

    return {
        dateIso: datePart,                   // "2025-12-02"
        dateDisplay: `${day}.${month}.${year}`, // "02.12.2025"
        time: `${hour}:${minute}`            // "01:45"
    };
}
function splitDateTime(isoString) {
    const d = new Date(isoString);

    // UTC günü almak
    const year = d.getUTCFullYear();
    const month = String(d.getUTCMonth() + 1).padStart(2, "0");
    const day = String(d.getUTCDate()).padStart(2, "0");

    // ISO (backend)
    const dateIso = `${year}-${month}-${day}`;  // 2025-11-25

    // Flatpickr görüntü (frontend)
    const dateDisplay = `${day}.${month}.${year}`; // 25.11.2025

    // Saat
    const hours = String(d.getUTCHours()).padStart(2, "0");
    const minutes = String(d.getUTCMinutes()).padStart(2, "0");
    const time = `${hours}:${minutes}`;

    return { dateIso, dateDisplay, time };
}

function hasOption(selectEl, value) {
    return !!selectEl.querySelector(`option[value="${value}"]`);
}
async function syncNormalToFullEdit() {

    $('#txt-task-name').val(globalEditTask.name);
    $('#txt-task-description').val(globalEditTask.description);

    var workflowId = globalEditTask?.workflowId || "";
    $('#ddl-task-workflow').val(workflowId).trigger('change');

    $('#ddl-task-category').val(globalEditTask.categoryId).trigger('change');
    $('#ddl-task-status').val(globalEditTask.statusId).trigger('change');
    $('#ddl-task-priority').val(globalEditTask.priorityId).trigger('change');
    $('#txt-task-estimated-hour').val(globalEditTask.estimatedHour);
    const attendeeIds = (globalEditTask?.assignee || []).map(a => a.id);
    $('#ddl-task-assignee').val(attendeeIds).trigger('change');
    if (globalEditTask?.endDate && fpFullTaskDue) {
        const dateObj = new Date(globalEditTask.endDate);
        fpFullTaskDue.setDate(dateObj, true);
    }
    if (globalEditTask?.startDate && fpFullTaskStart) {
        const dateObjStart = new Date(globalEditTask.startDate);
        fpFullTaskStart.setDate(dateObjStart, true);
    }
    await initSubTaskUI();
    subTaskList = globalEditTask.subTasks || [];
    renderSubTaskList();
    updateSubTaskMax();

    updateDependenciesTaskFormFields();
    dependenciesTasks = globalEditTask.dependencies || [];
    renderAllDependenciesTasks();

    $('#txt-checklist-name').val('');
    $('#txt-checklist-description').val('');
    checklistTasks = globalEditTask.checklist || [];
    renderAllChecklistTasks();
}
    let recordIdToDelete = null;

document.addEventListener('click', async function (e) {

        const btn = e.target.closest('.delete-task');
        if (!btn) return;

        recordIdToDelete = btn.getAttribute('data-id');

        // Modal açalım
        const tasksToDelete = getTasksToDelete(recordIdToDelete); // sadece UI listesi
        showDeleteWarningBootstrap(tasksToDelete, () => {

            deleteTaskById(recordIdToDelete)
                .then(() => {
                 
                   showToast("Task deleted successfully!", "error");
                    
                    refreshTaskOverview();
                })
                .catch(err => console.error(err));
        });
    });


    // 📌 Confirm Delete – API çağrısı burada yapılır

async function deleteTaskById(taskId) {

    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/DeleteTask`;

    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ id: taskId })
    });

    return await response.json();
}
function showDeleteWarningBootstrap(tasksToDelete, onConfirm) {

    const modalEl = document.getElementById("deleteTaskModal");
    const modal = new bootstrap.Modal(modalEl);

    // Listeyi doldur
    const listContainer = document.getElementById("deleteTaskList");
    listContainer.innerHTML = "";

    tasksToDelete.forEach(t => {
        listContainer.innerHTML += `
            <div class="list-group-item">${t.name}</div>
        `;
    });

    // Confirm butonu
    const btnConfirm = document.getElementById("confirmTaskDelete");
    btnConfirm.onclick = function () {

        modal.hide();

        if (typeof onConfirm === "function") {
            onConfirm();
        } else {
            console.warn("Confirm callback tanımlı değil!");
        }
    };

    modal.show();
}
function getTasksToDelete(id) {
    const toDelete = [];

    function collect(taskId) {
        // Ana task
        const task = taskOverviewList.find(t => t.id === taskId);
        if (task) toDelete.push(task);

        // Alt taskları bul
        const children = taskOverviewList.filter(t => t.parentTaskId === taskId);
        for (const child of children) {
            collect(child.id); // recursion
        }
    }

    collect(id);

    return toDelete;
}

function updateTaskDashboardCards() {

    // Normalize task list (subTasks hariç)
    const tasks = taskOverviewList.filter(t => Number(t.typeId) !== 2);

    const total = tasks.length;

    // 2 = In Progress, 3 = Completed
    const inProgress = tasks.filter(t => Number(t.statusId) === 2).length;
    const completed = tasks.filter(t => Number(t.statusId) === 3).length;

    const completionRate = total > 0
        ? Math.round((completed / total) * 100)
        : 0;

    // UI Update
    document.getElementById("totalTaskCount").textContent = total;
    document.getElementById("inProgressCount").textContent = `${inProgress} in progress`;

    document.getElementById("completedTaskCount").textContent = completed;
    document.getElementById("completionRate").textContent = `${completionRate}% completion rate`;
}
// Add Subtask modal açma

function fillSubTaskModal(taskId) {
    const task = taskOverviewList.find(t => t.id === taskId);
    if (!task) return;

    $("#m-sub-name").val("");
    $("#m-workflow").val(task.workflowName || "-");
}