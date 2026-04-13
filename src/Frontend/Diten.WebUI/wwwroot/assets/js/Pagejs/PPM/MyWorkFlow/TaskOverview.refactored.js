'use strict';

const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const port2 = protocol === 'https:' ? '5055' : '5050';
const userName = window.getUserName();
let taskOverviewList = [];     // API’den gelen gerçek liste
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

    // Eğer DataTable daha önce oluşturulmamışsa
    if (!dt_task_table) {

        dt_task_table = new DataTable(tableElem, {
            data: taskOverviewList,
            columns: [
                { data: 'id' },          // hidden
                { data: 'name' },        // Name
                { data: 'typeName' },
                { data: 'categoryName' },
                { data: 'workflowName' },
                { data: null },
                { data: 'priorityName' },
                { data: 'progress' },
                {data: null },
                {
                    data: null,
                    render: row => `${row.completedHour || 0}/${row.estimatedHour}`
                },
                { data: 'statusName' },
                {
                    data: null,
                    orderable: false,
                    render: renderTaskActions
                }
            ],
            columnDefs: [
                {
                    className: 'control',
                    responsivePriority: 2,
                    searchable: false,
                    targets: 0,
                    render: function () {
                        return '';
                    }
                },
                {
                    targets: 1,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        const name = full['name'] || '';
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
                    responsivePriority: 2,
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
                    responsivePriority: 1,
                    render: (data, type, full) => {

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
                                                columns: [1, 2, 3, 4, 5, 6, 7, 8],
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
                                        },
                                        {
                                            extend: 'excel',
                                            text: `<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-1"></i>Excel</span>`,
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
                                        },
                                        {
                                            extend: 'pdf',
                                            text: `<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-pdf me-1"></i>Pdf</span>`,
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

function renderTaskActions(row) {
    const id = row.id;

    // Owner → Edit + Delete
    if (row.canEditMainFields) {
        return `
            <div class="d-flex align-items-center">
                <a href="javascript:;" class="btn btn-icon edit-task" data-id="${id}">
                    <i class="bx bx-edit-alt"></i>
                </a>
                <a href="javascript:;" class="btn btn-icon delete-task" data-id="${id}">
                    <i class="bx bx-trash"></i>
                </a>
            </div>
        `;
    }

    // Meeting but bana atanmış → sadece Accept/Maybe/Decline
    if (row.typeId === 2 && row.canRespondToMeeting) {
        return `
            <div class="d-flex gap-2">
                <button class="btn btn-sm btn-success meeting-accept" data-id="${id}">Accept</button>
                <button class="btn btn-sm btn-warning meeting-maybe" data-id="${id}">Maybe</button>
                <button class="btn btn-sm btn-danger meeting-decline" data-id="${id}">Decline</button>
            </div>
        `;
    }

    // Bana atanan task (meeting değil) → sadece Status güncelleme
    if (!row.canEditMainFields && row.canChangeStatus) {
        return `
            <span class="badge bg-primary">Readonly</span>
            <a href="javascript:;" class="btn btn-icon update-status" data-id="${id}">
                <i class="bx bx-transfer-alt"></i>
            </a>
        `;
    }

    // Full readonly
    return `<span class="badge bg-secondary">No Permission</span>`;
}

async function refreshTaskOverview() {
    //const filterModel = collectFilterFormValues(); // senin filter formun
    const filterModel = defaultFilter;
    await loadTaskOverview(filterModel);
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
    $assignee.trigger("change");
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

    $el.trigger("change");
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
    const startVal = $("#add-meeting-start-time").val();
    const endVal = $("#add-meeting-end-time").val();

    const $start = $("#add-meeting-start-time");
    const $end = $("#add-meeting-end-time");

    // --- 1) START seçilmişse END options kontrol edilir ---
    if (startVal) {
        $("#add-meeting-end-time option").each(function () {
            const val = $(this).val();

            if (val < startVal) {
                $(this).prop("disabled", true);
            } else {
                $(this).prop("disabled", false);
            }
        });

        // End value start'tan küçük ise resetlenir
        if (endVal && endVal < startVal) {
            $end.val(null).trigger("change");
        }
    }

    // --- 2) END seçilmişse START options kontrol edilir ---
    if (endVal) {
        $("#add-meeting-start-time option").each(function () {
            const val = $(this).val();

            if (val > endVal) {
                $(this).prop("disabled", true);
            } else {
                $(this).prop("disabled", false);
            }
        });

        // Start value end’den büyükse resetlenir
        if (startVal && startVal > endVal) {
            $start.val(null).trigger("change");
        }
    }

    // Select2 refresh
    $start.trigger("change.select2");
    $end.trigger("change.select2");
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

        const dueDate = $('#add-task-due-date').val();
        payload.startDateTime = new Date();
        payload.endDateTime = dueDate ? getISODate(dueDate) : null;
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

    // JS Date → otomatik local timezone ile üretir
    const dt = new Date(yyyy, mm - 1, dd, hh, min, 0, 0);

    return dt;
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


    await initFullFormUI();
    resetChecklistTaskFormFields();


    initSubTaskUI();
    updateDependenciesTaskFormFields();



    syncNormalToFullForm();
    // 🔥 artık fonksiyonu kullanıyoruz
    switchToFullForm();

    if (taskTypeVal === '2') {
        await  initFullFormUI_Meeting();
        // MEETING
        syncNormalToFullForm_Meeting();
        //showFullFormPanelsForMeeting();
        //syncNormalToFullForm_Agenda();
    }



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

        // Name
        $('#txt-meeting-name').val($('#add-task-name').val());

        // Description
        $('#txt-meeting-description').val($('#add-task-description').val());

        // Workflow (text)
        $('#txt-workflow').val($('#add-task-workflow option:selected').text());

        // Category
        $('#ddl-meeting-category').val($('#add-task-category').val()).trigger('change');

        // Attendees
        $('#ddl-meeting-attendees')
            .val($('#add-meeting-attendees').val())
            .selectpicker('refresh');

        // Start Date
        const sDate = $('#add-meeting-start-date').val();
        if (sDate && fpMeetingStartFull) fpMeetingStartFull.setDate(sDate, true);

        // Start Time
        $('#ddl-meeting-start-time')
            .val($('#add-meeting-start-time').val())
            .trigger('change');

        // End Date
        const eDate = $('#add-meeting-end-date').val();
        if (eDate && fpMeetingEndFull) fpMeetingEndFull.setDate(eDate, true);

        // End Time
        $('#ddl-meeting-end-time')
            .val($('#add-meeting-end-time').val())
            .trigger('change');
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

    // Meeting bölümünü doldur
    await loadFullFormMeetingCategories();
    await loadFullFormMeetingAttendees();
    loadFullFormTimeDropdowns();
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

    const start = $('#dtSubTaskStartDate').val();
    const due = $('#dtSubTaskDueDate').val();
    const estimated = $('#txt-sub-task-estimated-hour').val();

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
        typeName: "Task"
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

    fpSubTaskStart.clear();
    fpSubTaskDue.clear();

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


    populateSelect('ddl-dependencies-task', {
        data: taskOverviewList,
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

function resetDependenciesTaskFormFields() {

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

function resetChecklistTaskFormFields() {
    checklistTasks = [];
    $("#txt-checklist-name").val('');
    $("#txt-checklist-description").val('');
    renderAllChecklistTasks();
}


//-------------------------- CREATE TASK BUTTON -------------------------//
document.getElementById("btnCreateTask")?.addEventListener("click", async function () {

    const mode = this.getAttribute("data-mode") || "create";
    const editId = this.getAttribute("data-edit-id");

    if (mode === "edit" && editId) {

        updateTask(editId);
    }
    else {

        createTask();

    }

   // updateTaskDashboardCards();

    resetChecklistTaskFormFields();
    resetDependenciesTaskFormFields();
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
        startDateTime = $("#dtTaskStartDate").val() ? new Date($("#dtTaskStartDate").val()) : new Date();
        endDateTime = $("#dtTaskDueDate").val() ? new Date($("#dtTaskDueDate").val()) : null;
        intiativeId = $('#ddl-task-workflow').val();
        //intiativeName = $('#ddl-task-workflow option:selected').text();



    }
    else {

        name = $('#txt-meeting-name').val();
        categoryId = $('#ddl-meeting-category').val();
        categoryName = $('#ddl-meeting-category option:selected').text();
        description = $('#txt-meeting-description').val();
        priorityId = 0;
        priorityName = "-";
        estimatedHour = 0;
        location = $('#txt-meeting-location').val();
        meetingLink = $('#txt-meeting-link').val();
        isVirtual = $('#checkVirtualMeeting').is(':checked');
        assigneeIds = $('#ddl-meeting-attendees').val() || [];
        const startDate = $("#txt-meeting-start-date").val();
        const startTime = $("#ddl-meeting-start-time").val();
        const endDate = $("#txt-meeting-end-date").val();
        const endTime = $("#ddl-meeting-end-time").val();

        startDateTime = getCombinedDateTime(startDate, startTime);
        endDateTime = getCombinedDateTime(endDate, endTime);
        agendaItems = getAgendaItems();
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

            resetDependenciesTaskFormFields();
            resetSubTaskForm();
            resetChecklistTaskFormFields();
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

function updateTask(taskId) {
    const idx = dummyTaskData.findIndex(t => t.id === taskId);
    if (idx === -1) return;

    let task = dummyTaskData[idx];

    const taskTypeId = getTaskTypeFromTabs();
    const taskTypeName = taskTypeId === "1" ? "Task" : "Meeting";

    const newSubTasks = subTasks.map(st => ({
        ...st,
        parentTaskId: taskId
    }));
    const newDependencies = dependenciesTasks.map(st => ({
        ...st,
        taskId: taskId
    }));
    const newChecklist = checklistTasks.map(ch => ({
        ...ch,
        taskId: taskId
    }));
    // Yeni değerleri oku (createTask'taki gibi)
    // -----------------------------------------
    let name, categoryId, categoryName, description, assignee, startDateTime,
        endDateTime, priorityId, priorityName, statusId, statusName,
        estimatedHour, location, meetingLink, checkVirtualMeetingLink, agendaItems;

    if (taskTypeId === "1") {
        // TASK Update
        name = $('#txt-task-name').val();
        description = $('#txt-task-description').val();
        categoryId = $('#ddl-task-category').val();
        categoryName = $('#ddl-task-category option:selected').text();
        const selectedAssigneeId = $('#ddl-task-assignee').val();

        assignee = selectedAssigneeId
            ? [{
                id: selectedAssigneeId,
                name: $('#ddl-task-assignee option[value="' + selectedAssigneeId + '"]').text()
            }]
            : [];

        priorityId = $('#ddl-task-priority').val();
        priorityName = $('#ddl-task-priority option:selected').text();
        statusId = $('#ddl-task-status').val();
        statusName = $('#ddl-task-status option:selected').text();
        estimatedHour = parseInt($('#txt-task-estimated-hour').val()) || 0;

        startDateTime = new Date($("#dtTaskStartDate").val());
        endDateTime = new Date($("#dtTaskDueDate").val());

        location = "";
        meetingLink = "";
        checkVirtualMeetingLink = false;

        agendaItems = [];

    } else {
        // MEETING Update
        name = $('#txt-meeting-name').val();
        description = $('#txt-meeting-description').val();

        categoryId = $('#ddl-meeting-category').val();
        categoryName = $('#ddl-meeting-category option:selected').text();

        const attendees = $('#ddl-meeting-attendees').val() || [];
        assignee = attendees.map(id => ({
            id: id,
            name: $('#ddl-meeting-attendees option[value="' + id + '"]').text()
        }));

        priorityId = 0;
        priorityName = "-";
        statusId = 0; // Meetings may not use status
        estimatedHour = 0;
        location = $('#txt-meeting-location').val();
        meetingLink = $('#txt-meeting-link').val();
        checkVirtualMeetingLink = $('#checkVirtualMeeting').is(':checked');

        const startDate = $("#txt-meeting-start-date").val();
        const startTime = $("#ddl-meeting-start-time").val();
        const endDate = $("#txt-meeting-end-date").val();
        const endTime = $("#ddl-meeting-end-time").val();

        startDateTime = getCombinedDateTime(startDate, startTime);
        endDateTime = getCombinedDateTime(endDate, endTime);

        agendaItems = getAgendaItems();
    }

    // Task objesini güncelle
    task = {
        ...task,
        name,
        description,
        categoryId,
        categoryName,
        assignee,
        startDateTime,
        endDateTime,
        priorityId,
        priorityName,
        statusId,
        statusName,
        estimatedHour,
        location,
        meetingLink,
        isVirtual: checkVirtualMeetingLink,
        agendaItems,
        typeId: taskTypeId,
        typeName: taskTypeName,
        subTasks: newSubTasks,
        dependenciesTasks: newDependencies,
        checklistTasks: newChecklist
    };

    dummyTaskData[idx] = task;

    newSubTasks.forEach(st => {
        const exists = dummyTaskData.some(t => t.id === st.id);
        if (!exists) {

            const normalized = normalizeSubTask(st, task);
            dummyTaskData.push(normalized);

        }
    });

    // 2️⃣ Bu taskı subtask olarak kullanan tüm parent taskları da güncelle
    dummyTaskData.forEach(parent => {

        if (Array.isArray(parent.subTasks)) {
            parent.subTasks = parent.subTasks.map(st => {
                if (st.id === taskId) {
                    // 🔥 Subtask olarak geçen bu kayıt da güncellenmeli
                    return {
                        ...st,
                        parentTaskId: st.parentTaskId,
                        name: task.name,
                        description: task.description,
                        estimatedHour: task.estimatedHour,
                        priorityId: task.priorityId,
                        priorityName: task.priorityName,
                        statusId: task.statusId,
                        statusName: task.statusName,
                        categoryId: task.categoryId,
                        categoryName: task.categoryName,
                        assignee: task.assignee,
                        startDateTime: task.startDateTime,
                        endDateTime: task.endDateTime
                    };
                }
                return st;
            });
        }

    });

    // DataTable update
    if (dt_workflow_task) {
        dt_workflow_task
            .clear()
            .rows.add(dummyTaskData)
            .draw();
    }

    showToast("Task updated successfully!", "success");


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
$(document).on('click', '.edit-task', function () {
    const id = $(this).data('id'); // satır ID'si

    //const task = dummyTaskData.find(m => m.id === id);
    //const recordType = urlParams.get('recordType') || '';

    const btn = document.getElementById("btnCreateTask");
    btn.textContent = "Update Task";
    btn.setAttribute("data-mode", "edit");
    btn.setAttribute("data-edit-id", task.id);

    document.getElementById("btnCreateTask").textContent = "Update Task";
/*    const taskTypeText = task?.typeName || 'Task';*/
    document.getElementById('hdrTask').textContent = `Edit ${taskTypeText} / ${task.name}`;
    document.getElementById('pTask').textContent =
        `Edit a  ${taskTypeText} to your workspace and assign it to a ${recordType} and group`;

    handleTaskTabs(String(task.typeId));
    if (String(task.typeId) === '1') toggleForms('taskFormContainer', 'meetingFormContainer');
    if (String(task.typeId) === '2') toggleForms('meetingFormContainer', 'taskFormContainer');
/*    updateEditFormFields(String(task.typeId), task);*/

    document.getElementById('normalFormContainer').classList.add('d-none');
    document.getElementById('fullFormContainer').classList.remove('d-none');
});
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
        typeName: st.typeName
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
        }
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

    workflows.forEach(w => {
        $wf.append(`<option value="${w.id}">${w.name}</option>`);
    });

    $wf.trigger("change");
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

    const startVal = $("#ddl-meeting-start-time").val();
    const endVal = $("#ddl-meeting-end-time").val();

    const $start = $("#ddl-meeting-start-time");
    const $end = $("#ddl-meeting-end-time");

    // 1) Start seçilmişse End kontrol edilir
    if (startVal) {
        $("#ddl-meeting-end-time option").each(function () {
            const val = $(this).val();

            if (val < startVal) $(this).prop("disabled", true);
            else $(this).prop("disabled", false);
        });

        if (endVal && endVal < startVal) {
            $end.val(null).trigger("change");
        }
    }

    // 2) End seçilmişse Start kontrol edilir
    if (endVal) {
        $("#ddl-meeting-start-time option").each(function () {
            const val = $(this).val();

            if (val > endVal) $(this).prop("disabled", true);
            else $(this).prop("disabled", false);
        });

        if (startVal && startVal > endVal) {
            $start.val(null).trigger("change");
        }
    }

    $start.trigger("change.select2");
    $end.trigger("change.select2");
}
$(document).on("change", "#ddl-meeting-start-time", function () {
    validateMeetingTimesFull();
});

$(document).on("change", "#ddl-meeting-end-time", function () {
    validateMeetingTimesFull();
});

