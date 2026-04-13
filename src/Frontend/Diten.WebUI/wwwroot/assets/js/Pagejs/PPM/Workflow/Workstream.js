'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const port2 = protocol === 'https:' ? '5055' : '5050';
const userName = window.getUserName();
let dt_workstream_table;
let dtWorkstream = null;
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
const urlParams = new URLSearchParams(window.location.search);
const pageMode = urlParams.get('mode') || 'create';
const workflowId = window.location.pathname.split('/')[2];
const fullToolbar = [
    [
        {
            font: []
        },
        {
            size: []
        }
    ],
    ['bold', 'italic', 'underline', 'strike'],
    [
        {
            color: []
        },
        {
            background: []
        }
    ],
    [
        {
            script: 'super'
        },
        {
            script: 'sub'
        }
    ],
    [
        {
            header: '1'
        },
        {
            header: '2'
        },
        'blockquote',
        'code-block'
    ],
    [
        {
            list: 'ordered'
        },
        {
            indent: '-1'
        },
        {
            indent: '+1'
        }
    ],
    [{ direction: 'rtl' }, { align: [] }],
    ['link', 'image', 'video', 'formula'],
    ['clean']
];
let workstreamDescriprion;
workstreamDescriprion = new Quill('#workstream-description', {
    bounds: '#workstream-description',
    placeholder: 'Type Something...',
    modules: {
        syntax: true,
        toolbar: fullToolbar
    },
    theme: 'snow'
});
const btn = document.getElementById("btnToggleCanvasFullscreen");
const canvas = document.getElementById("canvasWorkstreamCreation");
const icon = btn.querySelector("i");
btn.addEventListener("click", function () {
    const isFullscreen = canvas.classList.toggle("fullscreen");

    // icon değiştir
    icon.className = isFullscreen
        ? "bx bx-collapse-alt"
        : "bx bx-expand-alt";

    // aria-label güncelle (accessibility)
    btn.setAttribute(
        "aria-label",
        isFullscreen ? "Collapse form" : "Expand form"
    );
});
function exitFullscreen() {
    canvas.classList.remove("fullscreen");
    icon.className = "bx bx-expand-alt fs-5";
    btn.setAttribute("aria-label", "Expand form");
}
// ESC ile fullscreen'den çık
document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && canvas.classList.contains("fullscreen")) {
        exitFullscreen();
    }
})

//----- Workstream Canvas Task & Meeting Fields Toggle -----//
const taskFields = document.querySelector(".task-fields");
const meetingFields = document.querySelector(".meeting-fields");

const dueDatePicker = flatpickr('#workstream-duedate', {
    dateFormat: "d.m.Y",   // ✔ dd.MM.yyyy formatı
    allowInput: true,
    minDate: 'today',
    static: true,
    appendTo: document.body,
    position: 'auto',
    onOpen: function (selectedDates, dateStr, instance) {
        instance.calendarContainer.classList.add('flatpickr-above-force');
    }

});

const startInput = canvas.querySelector('#meeting-start-date');
const endInput = canvas.querySelector('#meeting-end-date');

let endDatePicker = flatpickr(endInput, {
    dateFormat: 'Y-m-d',
    altInput: true,
    altFormat: 'd M Y',
    allowInput: true,
    minDate: 'today',
    appendTo: document.body,
    position: 'auto',
    static: true,
    onOpen(selectedDates, dateStr, instance) {
        instance.calendarContainer.classList.add('flatpickr-above-force');
    },
    onChange() {
        validateMeetingTimes();
    }
});

let startDatePicker = flatpickr(startInput, {
    dateFormat: 'Y-m-d',
    altInput: true,
    altFormat: 'd M Y',
    allowInput: true,
    minDate: 'today',
    appendTo: document.body,
    position: 'auto',
    static: true,
    onOpen(selectedDates, dateStr, instance) {
        instance.calendarContainer.classList.add('flatpickr-above-force');
    },
    onChange(selectedDates) {
        if (selectedDates.length && endDatePicker) {
            endDatePicker.set('minDate', selectedDates[0]);
            validateMeetingTimes();
        }
    }
});
function generateTimeOptions(selectId) {
    const $select = $(selectId);

    if ($select.hasClass("select2-hidden-accessible")) {
        $select.select2("destroy");
    }

    // 🔥 varsa eski container’ı da temizle
    $select.next('.select2-container').remove();

    // select’i temizle
    $select.empty().append('<option></option>');


    for (let h = 0; h < 24; h++) {
        for (let m = 0; m < 60; m += 15) {
            const hh = String(h).padStart(2, '0');
            const mm = String(m).padStart(2, '0');
            const time = `${hh}:${mm}`;
            $select.append(`<option value="${time}">${time}</option>`);
        }
    }

    $select.select2({
        dropdownParent: $('#canvasWorkstreamCreation'),
        placeholder: 'Select type',
        allowClear: true,
        width: '100%',
    });

    $select.val(null).trigger('change');
}
let fv;

function toggleFieldsByType(type) {

   
    // önce hepsini kapat
    taskFields.style.display = "none";
    meetingFields.style.display = "none";

    if (!type) return;

    const typeVal = Number(type); // veya Number(this.value)
    switch (typeVal) {
        case 1:
            taskFields.style.display = "block";
            break;

        case 2:
            meetingFields.style.display = "block";
            break;
    }
}
const typeSelect = document.getElementById("workstream-type");
// normal select change
typeSelect.addEventListener("change", function () {
    toggleFieldsByType(this.value);
});

// Select2 kullanıldığı için güvenli olsun diye
if (window.$ && $(typeSelect).hasClass("select2")) {
    $(typeSelect).on("select2:select select2:clear", function (e) {
        toggleFieldsByType(e.params?.data?.id || null);
    });
}

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

function truncateDescription(html, maxLength = 80) {
    const plain = htmlToPlainText(html);
    return smartTruncate(plain, maxLength);
}
function initWorkstreamTable() {

    const tableSelector = '.workstream-table';
    const apiUrl = `${protocol}//${domain}:${port}/services/DitenPPM/Workstream/GetParentTasksByWorkflow/${workflowId}`;
    //const workflowId = selectedWorkflowId; // sayfa context’inden geliyor
    let tableTitle = document.createElement('h5');
    tableTitle.classList.add('card-title', 'mb-0', 'text-nowrap', 'text-md-start', 'text-center');
    tableTitle.innerHTML = 'Workstream Overview';
    const tableEl = document.querySelector(tableSelector);
    if (!tableEl) return;

    // tekrar init edilmesin
    if (dtWorkstream) {
        dtWorkstream.destroy();
        dtWorkstream = null;
    }

    dtWorkstream = new DataTable(tableEl, {
        ajax: {
            url: apiUrl,
            type: 'GET',
            dataSrc: 'data'
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
            { data: null }            // Action
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

                    const workstreamId = row.id; 
                    const params = new URLSearchParams(window.location.search);
                    const projectName = params.get("workstreamName");
                    const workstreamName = encodeURIComponent(row.name);

                    const detailUrl =
                        `/ppm/${workstreamId}/workstream-tasks` +
                        `?projectName=${projectName}` +
                        `&workstreamName=${workstreamName}&workFlowId=${workflowId}`;

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
                <a href="${detailUrl}"
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
            <span class="badge bg-label-${color} text-nowrap">
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


            // Progress (placeholder)
            {
                targets: 4, // Progress column index
                responsivePriority: 1,
                orderable: false,
                render: function (data, type, row) {

                    const totalTasks = row.totalTasks || 0;
                    const completedTasks = row.completedTasks || 0;

                    // Progress yüzdesi
                    const progress = totalTasks > 0
                        ? Math.round((completedTasks / totalTasks) * 100)
                        : 0;

                    // SEARCH & SORT için düz text döndür
                    if (type === 'filter' || type === 'sort') {
                        return `${completedTasks}/${totalTasks} ${progress}%`;
                    }

                    // DISPLAY (Academy style)
                    return `
            <div class="d-flex align-items-center gap-3">
                <!-- % -->
                <p class="fw-medium mb-0 text-heading" style="min-width:40px;">
                    ${progress}%
                </p>

                <!-- Progress bar -->
                <div class="progress w-100" style="height:8px;">
                    <div
                        class="progress-bar"
                        style="width:${progress}%"
                        aria-valuenow="${progress}"
                        aria-valuemin="0"
                        aria-valuemax="100">
                    </div>
                </div>

                <!-- completed / total -->
                <small class="text-nowrap">
                    ${completedTasks}/${totalTasks}
                </small>
            </div>
        `;
                }
            },
            // Priority
            {
                targets: 5, // Priority column
                responsivePriority: 1,
                render: function (data, type, full) {

                    const priorityId = Number(full.priorityId);
                    const priorityName = full.priorityName || '';
                    // SEARCH & SORT için düz text
                    if (type === 'filter' || type === 'sort') {
                        return data || '';
                    }

                    let color = 'secondary';

                    switch (priorityId) {
                        case 1: // Low
                            color = 'primary';
                            break;
                        case 2: // Medium
                            color = 'info';
                            break;
                        case 3: // High
                            color = 'warning';
                            break;
                        case 4: // Critical
                            color = 'danger';
                            break;
                        default:
                            color = 'secondary';
                    }

                    return `
            <span class="badge bg-label-${color} text-nowrap">
                ${priorityName || '-'}
            </span>
        `;
                }
            },
            // Date
            {
                targets: 6, // Date column
                render: (data, type, full) => {

                    const startDate = full.startDate ? moment(full.startDate) : null;
                    const endDate = full.endDate ? moment(full.endDate) : null;

                    const start = startDate ? startDate.format('DD MMM YY') : '-';
                    const end = endDate ? endDate.format('DD MMM YY') : '-';

                    // SEARCH & SORT için düz text
                    if (type === 'filter' || type === 'sort') {
                        return `${start} ${end}`;
                    }

                    // Status bilgisi
                    const isCompleted = full.statusName?.toLowerCase() === 'completed'
                        || full.statusId === 3; // <-- Completed StatusId (gerekirse düzelt)

                    // Varsayılan renk
                    let endColorClass = 'text-primary';

                    if (endDate && !isCompleted) {
                        const today = moment().startOf('day');
                        const diffDays = endDate.diff(today, 'days');

                        if (diffDays < 0) {
                            // Geçmiş & tamamlanmamış
                            endColorClass = 'text-danger';
                        } else if (diffDays <= 3) {
                            // Yaklaşan
                            endColorClass = 'text-warning';
                        } else {
                            // Daha zamanı var
                            endColorClass = 'text-primary';
                        }
                    }

                    if (isCompleted) {
                        endColorClass = 'text-success';
                    }

                    return `
            <div class="d-flex flex-column text-nowrap">
                <small class="text-muted">
                    ${start}
                </small>
                <small class="fw-medium ${endColorClass}">
                    ${end}
                </small>
            </div>
        `;
                }
            },

            // Status
            {
                targets: 7, // Status column
                responsivePriority: 1,
                render: function (data, type, full) {

                    const statusId = Number(full.statusId);

                    // SEARCH & SORT için düz text
                    if (type === 'filter' || type === 'sort') {
                        return data || '';
                    }

                    let color = 'secondary';

                    switch (statusId) {
                        case 3: // Completed
                            color = 'success';
                            break;
                        case 2: // In Progress
                            color = 'primary';
                            break;
                        case 1: // Pending / New
                            color = 'warning';
                            break;
                        case 4: // Cancelled / Rejected
                            color = 'danger';
                            break;
                        default:
                            color = 'secondary';
                    }

                    return `
            <span class="badge bg-${color} text-nowrap">
                ${data || '-'}
            </span>
        `;
                }
            },

            // Actions
            {
                targets: 8,
                orderable: false,
                searchable: false,
                render: (data, type, row) => `
                        <div class="dropdown">
                            <button class="btn btn-icon" data-bs-toggle="dropdown">
                                <i class="bx bx-dots-vertical-rounded"></i>
                            </button>
                            <div class="dropdown-menu dropdown-menu-end">
                                <a class="dropdown-item d-flex alig-items-center view-record" href="javascript:;" data-id="${row.id}"><i class="icon-sm bx bx-show me-2"></i>View</a>
                                <a class="dropdown-item edit-record d-flex alig-items-center" href="javascript:;" data-id="${row.id}"><i class="icon-sm bx bx-edit-alt me-2"></i> Edit</a>
                                <a class="dropdown-item open-record d-flex alig-items-center" href="#"><i class="icon-sm bx bx-door-open me-2"></i> Open</a>
                                <a class="dropdown-item delete-record d-flex alig-items-center text-danger" href="#"><i class="icon-sm bx bx-trash me-2"></i> Delete</a>
                            </div>
                        </div>
                    `
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
                            },
                            {
                                text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">Add Workstream</span>',
                                className: 'add-new btn btn-primary',
                                action: function () {
                                    openCreateWorkstreamCanvas(); // 🔥 tek giriş noktası
                                }
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
            fixDataTableLayout();
          


        },
        drawCallback: function () {
            fixDataTableLayout(); // her yeniden çizimde stil uygula (sayfalama, filtre vs.)
        }
    });
}

function fixDataTableLayout() {
    const elementsToModify = [
        { selector: '.dt-buttons .btn', classToRemove: 'btn-secondary' },
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
        { selector: '.dt-buttons', classToAdd: 'd-flex gap-4 mb-md-0 mb-6' },
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
function setBreadcrumbFromQuery() {
    const params = new URLSearchParams(window.location.search);
    const name = params.get("workstreamName");

    if (!name) return;

    const el = document.getElementById("breadcrumbActive");
    if (!el) return;

    el.textContent = capitalize(decodeURIComponent(name));
}

//-------------------------- Create Canvas --------------------------//
async function loadTypes() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskTypes`;
    const res = await fetch(url);
    const list = await res.json();

    const $el = $('#workstream-type');

     //Eski select2 instance varsa önce destroy et
    if ($el.hasClass("select2-hidden-accessible")) {
        $el.select2("destroy");
    }

    $el.empty();

    list.forEach(x => {
        let displayName = x.name;

        if (x.id === 1) {
            displayName = 'Workstream';
        }

        $el.append(`<option value="${x.id}">${displayName}</option>`);
    });

    $el.prepend('<option></option>');

    // ✅ Select2’yi yeniden init et
    $el.select2({
        dropdownParent: $('#canvasWorkstreamCreation'),
        placeholder: 'Select type',
        allowClear: true,
        width: '100%'
    });

    $el.val(null).trigger('change');
}
async function loadCategories() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskCategory`;
    const res = await fetch(url);
    const result = await res.json();

    const $el = $('#workstream-category');
    //Eski select2 instance varsa önce destroy et
    if ($el.hasClass("select2-hidden-accessible")) {
        $el.select2("destroy");
    }
    $el.empty();

    result.forEach(x => {
        $el.append(`<option value="${x.id}">${x.name}</option>`);
    });
    $el.prepend('<option></option>');
    $el.select2({
        dropdownParent: $('#canvasWorkstreamCreation'),
        placeholder: 'Select category',
        allowClear: true,
        width: '100%'
    });

    $el.val(null).trigger('change');
}
async function loadAssignee() {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const list = await res.json();

    const $attendees = $('#workstream-assignee');

    if ($attendees.hasClass("select2-hidden-accessible")) {
        $attendees.select2("destroy");
    }
    $attendees.empty();


    list.data.forEach(u => {
        const option = `<option value="${u.id}">${u.fullName}</option>`;
        $attendees.append(option);
    });

    $attendees.select2({
        dropdownParent: $('#canvasWorkstreamCreation'),
        placeholder: 'Select assignee / attendees',
        allowClear: true,
        width: '100%'
    });

    // 3) select2 refresh
    $attendees.trigger("change");

}
function getStatusBadge(statusId, statusName) {
    const map = {
        1: 'warning', // To do
        2: 'primary',   // In progress
        3: 'success',   // Completed
        4: 'danger'     // Canceled
    };

    const key = Number(statusId);
    const badgeClass = map[key] || 'secondary';

    return `
        <span class="badge bg-${badgeClass}">
            ${statusName}
        </span>
    `;
}
async function loadStatus() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`;
    const res = await fetch(url);
    const list = await res.json();

    const $el = $('#workstream-status');
    const $canvas2 = $('#canvasWorkstreamCreation'); // 🔥 offcanvas
    // Eski select2 instance varsa önce destroy et
    if ($el.hasClass("select2-hidden-accessible")) {
        $el.select2("destroy");
    }

    $el.empty();

    list.forEach(x => {

        var optionHtml = getStatusBadge(x.id, x.name);

        $el.append(`<option value="${x.id}"  data-status="${x.id}">${x.name}</option>`);
    });

    $el.select2({
        placeholder: 'Select status',
        allowClear: true,
        width: '100%',
        dropdownParent: $canvas2,
        templateResult: formatStatusOption,
        templateSelection: formatStatusOption,
        escapeMarkup: m => m // HTML render için ŞART
    });
    $el.val(null).trigger('change');

}
function formatStatusOption(state) {
    if (!state.id) return state.text;

    const statusId = state.element.dataset.status;
    return getStatusBadge(statusId, state.text);
}
function getPriorityBadge(priorityId, priorityName) {
    const map = {
        0: { icon: 'icon-base bx bx-minus', color: 'secondary' }, // None
        1: { icon: 'icon-base bx bx-chevrons-down', color: 'primary' },   // Low
        2: { icon: 'icon-base bx bx-menu', color: 'secondary' }, // Medium
        3: { icon: 'icon-base bx bx-chevron-up', color: 'warning' },   // High
        4: { icon: 'icon-base bx bx-chevrons-up', color: 'danger' }     // Critical
    };

    const key = Number(priorityId);
    const cfg = map[key] || map[2]; // default Medium

    return `
        <span class="d-flex align-items-center gap-2">
            <i class="${cfg.icon} text-${cfg.color} fs-5"></i>
            <span>${priorityName}</span>
        </span>
    `;
}
function formatPriorityOption(state) {
    if (!state.id) return state.text;
    return getPriorityBadge(state.id, state.text);
}
async function loadPriority() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetPriorities`;
    const res = await fetch(url);
    const list = await res.json();

    const $el = $('#workstream-priority');
    const $canvas2 = $('#canvasWorkstreamCreation');
    // Eski select2 instance varsa önce destroy et
    if ($el.hasClass("select2-hidden-accessible")) {
        $el.select2("destroy");
    }

    $el.empty();

    list.forEach(x => {
        $el.append(`<option value="${x.id}">${x.name}</option>`);
    });
    $el.select2({
        placeholder: 'Select priority',
        allowClear: true,
        width: '100%',
        dropdownParent: $canvas2, // 🔥 offcanvas için şart
        templateResult: formatPriorityOption,
        templateSelection: formatPriorityOption,
        escapeMarkup: m => m
    });
    $el.val(null).trigger('change');

}
function validateMeetingTimes() {
    const startDate = startDatePicker?.selectedDates[0];
    const endDate = endDatePicker?.selectedDates[0];

    const startTime = $('#meeting-start-time').val();
    const endTime = $('#meeting-end-time').val();

    // Eğer tarih veya saatler eksikse, validate etme
    if (!startDate || !endDate || !startTime || !endTime) return;

    // Aynı gün mü?
    if (startDate.toDateString() === endDate.toDateString()) {
        if (startTime >= endTime) {
            $('#meeting-end-time').val(null).trigger('change');
            alert('End Time must be later than Start Time');
        }
    }
}
$('#meeting-start-time').on('change', updateEndTimeOptions);
$('#meeting-start-date, #meeting-end-date').on('change', updateEndTimeOptions); function updateEndTimeOptions() {
    const startDate =
        startDatePicker &&
            Array.isArray(startDatePicker.selectedDates) &&
            startDatePicker.selectedDates.length > 0
            ? startDatePicker.selectedDates[0]
            : null;

    const endDate =
        endDatePicker &&
            Array.isArray(endDatePicker.selectedDates) &&
            endDatePicker.selectedDates.length > 0
            ? endDatePicker.selectedDates[0]
            : null;
    const startTime = $('#meeting-start-time').val();
    const $end = $('#meeting-end-time');

    // Reset all options
    $end.find('option').prop('disabled', false);

    // Eğer eksik veri varsa, kısıtlama yapma
    if (!startDate || !endDate || !startTime) {
        $end.trigger('change.select2');
        return;
    }

    // 🔴 SADECE AYNI GÜN İSE KISITLA
    if (startDate.toDateString() === endDate.toDateString()) {
        $end.find('option').each(function () {
            const val = $(this).val();
            if (val && val <= startTime) {
                $(this).prop('disabled', true);
            }
        });
    }

    $end.trigger('change.select2');
}

function resetWorkstreamCanvas() {

    // 1️⃣ Text inputlar
    $('#workStreamCreationForm')
        .find('input[type="text"]')
        .val('');

    //// 2️⃣ Select2'ler
    $('#workStreamCreationForm')
        .find('select')
        .val(null)
        .trigger('change');

    // 3️⃣ Flatpickr tarihleri
    // 3️⃣ Flatpickr tarihleri (instance kontrolü)
    if (dueDatePicker && typeof dueDatePicker.clear === 'function') {
        dueDatePicker.clear();
    }

    if (startDatePicker && typeof startDatePicker.clear === 'function') {
        startDatePicker.clear();
    }

    if (endDatePicker && typeof endDatePicker.clear === 'function') {
        endDatePicker.clear();
    }
    workstreamDescriprion?.setText('');
    // 4️⃣ Task / Meeting alanlarını gizle (Quick Create default)
    taskFields.style.display = "none";
    meetingFields.style.display = "none";

    // 5️⃣ End Time option disable reset
    $('#meeting-end-time option').prop('disabled', false);
    $('#meeting-end-time').trigger('change.select2');
    
    // 6️⃣ Validation mesajları temizle (FormValidation kullanıyorsan)
    $('.fv-plugins-message-container').empty();
}
let isCanvasInitializing = false;
function initializeFormValidation() {

    const workflowForm = document.getElementById('workStreamCreationForm');
    if (!workflowForm) return;

    fv = FormValidation.formValidation(workflowForm, {
        fields: {

            // 🔹 ZORUNLU
            workstreamName: {
                validators: {
                    notEmpty: {
                        message: 'Workstream name is required'
                    }
                }
            },

            // 🔹 OPSİYONEL ama seçilirse geçerli olmalı
            workstreamType: {
                validators: {
                    notEmpty: {
                        message: 'Workstream type is required'
                    },
                    callback: {
                        message: 'Invalid type selected',
                        callback: function () {
                            return true;
                        }
                    }
                }
            },

            workstreamCategory: {
                validators: {
                    notEmpty: {
                        message: 'Workstream category is required'
                    },
                    callback: {
                        message: 'Invalid category selected',
                        callback: function () {
                            return true;
                        }
                    }
                }
            },

            // 🔹 TASK TYPE için
            workstreamDuedate: {
                validators: {
                    callback: {
                        message: 'Due date is required for task',
                        callback: function () {
                            const type = $('#workstream-type').val();
                            if (type !== 'task') return true;
                            return !!$('#workstream-duedate').val();
                        }
                    }
                }
            },

            // 🔹 MEETING TYPE için
            meetingStartDate: {
                validators: {
                    callback: {
                        message: 'Start date is required',
                        callback: function () {
                            const type = $('#workstream-type').val();
                            if (type !== 'meeting') return true;
                            return !!$('#meeting-start-date').val();
                        }
                    }
                }
            },

            meetingEndDate: {
                validators: {
                    callback: {
                        message: 'End date must be after start date',
                        callback: function () {
                            const type = $('#workstream-type').val();
                            if (type !== 'meeting') return true;

                            const start = startDatePicker?.selectedDates[0];
                            const end = endDatePicker?.selectedDates[0];

                            if (!start || !end) return false;
                            return end >= start;
                        }
                    }
                }
            }
        },

        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: '',
                rowSelector: '.form-control-validation'
            }),
            submitButton: new FormValidation.plugins.SubmitButton(),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });

    // 🔹 Select2 değişince validate et
    $('#workstream-type, #workstream-category')
        .on('change', function () {
            if (isCanvasInitializing) return;   // 👈 KRİTİK SATIR
            fv.revalidateField(this.name);
        });

    // 🔹 Flatpickr değişince validate et
    $('#workstream-duedate, #meeting-start-date, #meeting-end-date')
        .on('change', function () {
            if (isCanvasInitializing) return;   // 👈 KRİTİK SATIR
            fv.revalidateField(this.name);
        });

    handleFormSubmit(fv);
}
function combineDateTime(date, time) {
    if (!date || !time) return null;
    return `${date}T${time}:00`;
}
function toIsoDateTime(dateObj, time = '23:59:00') {
    if (!dateObj) return null;

    const yyyy = dateObj.getFullYear();
    const mm = String(dateObj.getMonth() + 1).padStart(2, '0');
    const dd = String(dateObj.getDate()).padStart(2, '0');

    return `${yyyy}-${mm}-${dd}T${time}`;
}
function handleFormSubmit(fv) {
    fv.on('core.form.valid', async function () {

        const $form = $('#workStreamCreationForm');
        const mode = $form.attr('data-mode') || 'create';
        const workstreamId = $form.attr('data-id');



        const typeId = parseInt($('#workstream-type').val());

        let startDateTime = null;
        let endDateTime = null;

        // 🔹 TASK
        if (typeId === 1) {
            const fp = document.querySelector('#workstream-duedate')?._flatpickr;

            if (fp && fp.selectedDates.length > 0) {
                endDateTime = toIsoDateTime(fp.selectedDates[0], '23:59:00');
            }
        }

        // 🔹 MEETING
        if (typeId === 2) {
            startDateTime = combineDateTime(
                $('#meeting-start-date').val(),
                $('#meeting-start-time').val()
            );

            endDateTime = combineDateTime(
                $('#meeting-end-date').val(),
                $('#meeting-end-time').val()
            );
        }

        const basePayload = {
            workFlowId: workflowId,
            name: $('#workstream-name').val(),
            categoryId: parseInt($('#workstream-category').val(), 10),
            priorityId: parseInt($('#workstream-priority').val(), 10),
            statusId: parseInt($('#workstream-status').val(), 10),
            description: workstreamDescriprion?.root.innerHTML,
            assignees: $('#workstream-assignee').val() || [],
            startDateTime,
            endDateTime,
            estimatedHour: 0,
            isVirtual: false,
            location: null,
            meetingLink: null
        };

        let url;
        let method;
        let payload;

        // ===============================
        // ➕ CREATE
        // ===============================
        if (mode === 'create') {
            payload = {
                ...basePayload,
                typeId, // 👈 sadece create
                ownerId: window.getUserId(),
                createdBy: userName,
                isQuickCreated: true,
                isPlacedOnCalendar: false
            };

            url = `${protocol}//${domain}:${port}/services/DitenPPM/Workstream/CreateWorkstream`;
            method = 'POST';

            console.log('CREATE WORKSTREAM PAYLOAD', payload);
        }

        // ===============================
        // ✏️ UPDATE
        // ===============================
        else if (mode === 'edit') {
            payload = {
                ...basePayload,
                id: workstreamId,
                modifiedBy: userName
            };

            url = `${protocol}//${domain}:${port}/services/DitenPPM/Workstream/UpdateWorkstream`;
            method = 'POST';

            console.log('UPDATE WORKSTREAM PAYLOAD', payload);
        }



        try {
            const res = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                const err = await res.json();
                showToast(err?.message || 'Create workstream failed','error');
                return;
            }

            await res.json();

            showToast(
                mode === 'create'
                    ? 'Workstream created successfully'
                    : 'Workstream updated successfully',
                'success'
            );

            // 🔄 refresh
            const table = $('.workstream-table').DataTable();
            table.ajax.reload();

            // 🧹 reset
            resetWorkstreamCanvas();
            fv.resetForm(true);
            workstreamDescriprion?.setContents([]);

            // ❌ close offcanvas
            //bootstrap.Offcanvas
            //    .getInstance(document.getElementById('canvasWorkstreamCreation'))
            //    .hide();

        } catch (e) {
            console.error(e);
            showToast('Unexpected error occurred','error');
        }
        // 👉 burada API çağrısı yapılır
    });
}



document.getElementById('canvasWorkstreamCreation')
    .addEventListener('shown.bs.offcanvas', function () {
        isCanvasInitializing = false;
    });

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
$(document).on("click", ".edit-record", function () {

    const tr = $(this).closest("tr");
    //const tableElem = document.querySelector(".workstream-table");
    //const dt = tableElem?._dt;

    if (!dtWorkstream) {
        console.error("DataTable instance bulunamadı");
        return;
    }

    const rowData = dtWorkstream.row(tr[0]).data(); // 🔥 tr[0] DOM node

    if (!rowData) {
        console.error("Row data bulunamadı");
        return;
    }

    prepareWorkstreamEditMode(rowData);
});
function prepareWorkstreamEditMode(data) {

    // Başlık & buton
    $("#canvasWorkstreamCreationLabel").text("Edit Workstream");
    $(".data-submit").text("Update").show();

    $("#workStreamCreationForm")
        .attr("data-mode", "edit")
        .attr("data-id", data.id);

    $('#workstream-type')
        .prop('disabled', true)
        .trigger('change.select2');
    // Formu doldur
    fillWorkstreamForm(data);
    setWorkstreamFormReadonly(false);
    // Canvas aç
    const el = document.getElementById("canvasWorkstreamCreation");
    bootstrap.Offcanvas.getOrCreateInstance(el).show();
}
async function fillWorkstreamForm(data) {
    // TYPE
    await loadTypes();
    $('#workstream-type').val(String(data.typeId)) // 🔥 string önemli
        .trigger('change');

    // NAME
    $('#workstream-name').val(data.name);

    // CATEGORY
    $('#workstream-category').val(data.categoryId).trigger('change');

    // DESCRIPTION (Quill varsa)
    if (workstreamDescriprion) {
        workstreamDescriprion.root.innerHTML = data.description || '';
    }

    // ASSIGNEES
    $('#workstream-assignee')
        .val((data.assignees || []).map(x => x.id))
        .trigger('change');

    // STATUS
    $('#workstream-status').val(data.statusId).trigger('change');

    // PRIORITY
    $('#workstream-priority').val(data.priorityId).trigger('change');

    // DUE DATE
    if (dueDatePicker && data.endDate) {
        dueDatePicker.setDate(new Date(data.endDate), true);
    }

    // MEETING / TASK alanları
    if (data.typeId === 2) {
        $(".task-fields").hide();
        $(".meeting-fields").show();

        startDatePicker?.setDate(new Date(data.startDate), true);
        endDatePicker?.setDate(new Date(data.endDate), true);
    } else {
        $(".task-fields").show();
        $(".meeting-fields").hide();
    }
}
function openCreateWorkstreamCanvas() {
    isCanvasInitializing = true;
    fv.resetForm(true);
    resetWorkstreamCanvas();
    generateTimeOptions('#meeting-start-time');
    generateTimeOptions('#meeting-end-time');
    

    $("#canvasWorkstreamCreationLabel").text("Add Workstream");
    $(".data-submit").text("Add").show();

    $("#workStreamCreationForm")
        .attr("data-mode", "create")
        .removeAttr("data-id");

    $('#workstream-type')
        .prop('disabled', false)
        .val(null)
        .trigger('change.select2');
    setWorkstreamFormReadonly(false);

    bootstrap.Offcanvas
        .getOrCreateInstance("#canvasWorkstreamCreation")
        .show();
}

$(document).on("click", ".view-record", function () {

    const tr = $(this).closest("tr");
    //const tableElem = document.querySelector(".workstream-table");
    //const dt = tableElem?._dt;

    if (!dtWorkstream) {
        console.error("DataTable instance bulunamadı");
        return;
    }

    const rowData = dtWorkstream.row(tr[0]).data(); // 🔥 tr[0] DOM node

    if (!rowData) {
        console.error("Row data bulunamadı");
        return;
    }

    prepareWorkstreamViewMode(rowData);
});

function prepareWorkstreamViewMode(data) {

    // 🏷 Header
    $("#canvasWorkstreamCreationLabel").text("View Workstream");

    // ❌ Submit gizle
    $(".data-submit").hide();

    // mode = view
    $("#workStreamCreationForm")
        .attr("data-mode", "view")
        .attr("data-id", data.id);

    // Formu doldur
    fillWorkstreamForm(data);

    // 🔒 TÜM alanları disable
    setWorkstreamFormReadonly(true);

    // Canvas aç
    bootstrap.Offcanvas
        .getOrCreateInstance("#canvasWorkstreamCreation")
        .show();
}
function setWorkstreamFormReadonly(isReadonly) {

    // Inputs & textarea
    $('#workStreamCreationForm')
        .find('input, textarea, select')
        .prop('disabled', isReadonly);

    // Select2 refresh
    $('#workStreamCreationForm select')
        .trigger('change.select2');

    // Quill / description
    if (window.workstreamDescriprion) {
        workstreamDescriprion.enable(!isReadonly);
    }

    // Flatpickr
    document.querySelectorAll('input._flatpickr').forEach(el => {
        if (el._flatpickr) {
            el._flatpickr.set('clickOpens', !isReadonly);
        }
    });
}


document.addEventListener("DOMContentLoaded", async function () {
    setBreadcrumbFromQuery();
    loadTypes();
    loadCategories();
    loadAssignee();
    loadStatus();
    loadPriority();
    initWorkstreamTable();
    initializeFormValidation();
});
