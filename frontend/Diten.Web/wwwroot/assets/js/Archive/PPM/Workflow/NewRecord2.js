/* -------------------------------------------------------
   PERFORMANCE HELPERS – debounce / throttle
------------------------------------------------------- */

function debounce(fn, delay = 300) {
    let timer;
    return function (...args) {
        clearTimeout(timer);
        timer = setTimeout(() => fn.apply(this, args), delay);
    };
}

function throttle(fn, limit = 200) {
    let inThrottle = false;
    return function (...args) {
        if (!inThrottle) {
            fn.apply(this, args);
            inThrottle = true;
            setTimeout(() => inThrottle = false, limit);
        }
    };
}

function generateTimeOptions(start = "00:00", end = "23:45", stepMinutes = 15) {
    const options = [];

    const [startH, startM] = start.split(":").map(Number);
    const [endH, endM] = end.split(":").map(Number);

    let currentMinutes = startH * 60 + startM;
    const endMinutes = endH * 60 + endM;

    while (currentMinutes <= endMinutes) {

        const hours = Math.floor(currentMinutes / 60);
        const minutes = currentMinutes % 60;

        const ampm = hours >= 12 ? "PM" : "AM";
        const displayH = ((hours % 12) || 12);
        const displayM = minutes.toString().padStart(2, "0");

        const value = `${hours.toString().padStart(2, "0")}:${displayM}`;
        const text = `${displayH}:${displayM} ${ampm}`;

        options.push({ value, text });

        currentMinutes += stepMinutes;
    }

    return options;
}
function modifyDataTableLayout() {

    const changes = [
        { selector: '.dt-buttons .btn', remove: ['btn-secondary'], add: [] },
        { selector: '.dt-search .form-control', remove: ['form-control-sm'], add: [] },
        { selector: '.dt-length .form-select', remove: ['form-select-sm'], add: ['ms-0'] },
        { selector: '.dt-length', remove: [], add: ['mb-md-6', 'mb-0'] },
        { selector: '.dt-search', remove: [], add: ['mb-md-6', 'mb-2'] },
        { selector: '.dt-layout-end', remove: ['justify-content-between'], add: ['d-flex', 'gap-md-4', 'justify-content-md-between', 'justify-content-center', 'gap-4', 'flex-wrap', 'mt-0'] },
        { selector: '.dt-layout-start', remove: [], add: ['mt-0'] },
        { selector: '.dt-buttons', remove: [], add: ['d-flex', 'gap-4', 'mb-md-0', 'mb-6'] },
        { selector: '.dt-layout-table', remove: ['row', 'mt-2'], add: [] },
        { selector: '.dt-layout-full', remove: ['col-md', 'col-12'], add: ['table-responsive'] }
    ];

    for (const rule of changes) {
        document.querySelectorAll(rule.selector).forEach(el => {
            rule.remove.forEach(cls => el.classList.remove(cls));
            rule.add.forEach(cls => el.classList.add(cls));
        });
    }
}

async function loadWorkflow(workflowId) {
    try {
        const response = await fetch(
            `${window.ApiBaseUrl}/services/DitenPPM/Workflow/GetWorkflowById/${workflowId}`
        );

        if (!response.ok) throw new Error("API error");

        const result = await response.json();
        const data = result?.data;

        if (!data) {
            console.warn("Workflow not found.");
            resetGlobalWorkflowState();
            return;
        }

        // TEAM ve TASK verilerini dummyData üzerine yaz
        dummyData = data?.workFlowTeams || [];
        dummyTaskData = data?.workFlowTasks || [];

        // FORM ALANLARI
        $('#txt-name').val(data?.name || "");
        $('#txt-code').val(data?.idCode || "");
        $('#txt-description').val(data?.description || "");

        // STATUS DROPDOWN
        await populateSelect('ddl-status', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: data?.workflowStatusId
        });

        // PRIORITY DROPDOWN
        await populateSelect('ddl-priority', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
            placeholder: 'Select priority',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: data?.priorityId
        });

        // TEAM TABLOSU YENİLE
        if (typeof initTeamDataTable === "function") {
            initTeamDataTable("Search...", null);
        }

        // TASK TABLOSU YENİLE
        if (typeof initWorkFlowTaskDataTable === "function") {
            initWorkFlowTaskDataTable("Search...", null);
        }

        // Drag & drop tekrar aktif olsun
        enableTaskTableDragDrop?.();

        // Layout düzeltmeleri tekrar et
        modifyDataTableLayout?.();

        // selectpicker/select2 refresh
        $('.selectpicker').selectpicker('refresh');
        $('select.select2').trigger('change');

        console.log("Workflow loaded successfully.");

    } catch (err) {
        console.error("loadWorkflow error:", err);

        resetGlobalWorkflowState();

        showToast("Workflow could not be loaded.", "error");
    }
}


/* -------------------------------------------------------
   GLOBAL VARIABLES – TEMİZLENMİŞ & OPTİMİZE EDİLMİŞ
------------------------------------------------------- */
'use strict';
const port2 = protocol === 'https:' ? '5055' : '5050';
const userName = window.getUserName();
let dummyData = [];
let dummyTaskData = [];

let pendingAddToMainStart = null;
let pendingAddToMainEnd = null;
let pendingAttendees = [];   // reset gereken yerlerde sıfırlanacak

// Task alt yapıları

let fvDependenciesTask = null;
let fvChecklistTask = null;

// UI element references
const checkVirtual = document.querySelector("#checkVirtualMeeting");
const txtLocation = document.querySelector("#txt-meeting-location");
const txtLink = document.querySelector("#txt-meeting-link");

const txtTaskEstimated = document.querySelector("#txt-task-estimated-hour");
const subHourInput = document.querySelector("#txt-sub-task-estimated-hour");
let dt_workflow_task = null;
/* -------------------------------------------------------
   VIRTUAL MEETING TOGGLE – TEMİZ VERSİYON
------------------------------------------------------- */
checkVirtual?.addEventListener("change", function () {

    if (this.checked) {
        txtLocation.value = "";
        txtLocation.disabled = true;

        txtLink.disabled = false;
        txtLink.focus();
    } else {
        txtLocation.disabled = false;
        txtLink.value = "";
        txtLink.disabled = true;
    }
});

/* -------------------------------------------------------
   GLOBAL RESET FUNCTIONS
------------------------------------------------------- */

function resetAllTaskFormData() {
    subTasks = [];
    dependenciesTasks = [];
    checklistTasks = [];

    pendingAttendees = [];
    pendingAddToMainStart = null;
    pendingAddToMainEnd = null;

    // Input reset
    document.querySelectorAll(
        "#add-task-name, #add-task-description, #add-task-estimated-hour"
    ).forEach(el => el.value = "");

    // Select reset
    $("#add-task-type").val(null).trigger("change");
    $("#add-task-status").val(null).trigger("change");
    $("#add-task-priority").val(null).trigger("change");
    $("#add-task-assignee").val(null).trigger("change");

    // Meeting form reset
    $("#add-meeting-attendees").val([]).trigger("change");
    $("#add-meeting-start-date").val("");
    $("#add-meeting-end-date").val("");
    $("#add-meeting-start-time").val("").trigger("change");
    $("#add-meeting-end-time").val("").trigger("change");

    flatpickr("#add-meeting-start-date")?.clear();
    flatpickr("#add-meeting-end-date")?.clear();
}

/* -------------------------------------------------------
   FLATPICKR PICKERS – TEMİZ, HATASIZ, BAĞIMSIZ
------------------------------------------------------- */

let startDatePicker, endDatePicker, mainStartPicker, mainEndPicker;
let startTaskDatePicker, dueTaskDatePicker, subStartPicker, subDuePicker;

/* MAIN PICKERS */
mainStartPicker = flatpickr("#txt-meeting-start-date", {
    altInput: true,
    altFormat: "d.m.Y",
    dateFormat: "Y-m-d",
    static: true,
    onChange(selectedDates) {
        if (selectedDates.length > 0)
            mainEndPicker.set("minDate", selectedDates[0]);
    }
});

mainEndPicker = flatpickr("#txt-meeting-end-date", {
    altInput: true,
    altFormat: "d.m.Y",
    dateFormat: "Y-m-d",
    static: true,
    onChange(selectedDates) {
        if (selectedDates.length > 0)
            mainStartPicker.set("maxDate", selectedDates[0]);
    }
});

/* TASK PICKERS */
startTaskDatePicker = flatpickr("#txt-task-start-date", {
    altInput: true,
    altFormat: "d.m.Y",
    dateFormat: "Y-m-d",
    static: true,
    onChange(selectedDates) {
        if (selectedDates.length > 0) {
            dueTaskDatePicker.set("minDate", selectedDates[0]);

            // Due daha önce seçili ise kontrol et
            const dueVal = dueTaskDatePicker.selectedDates[0];
            if (dueVal && dueVal < selectedDates[0]) {
                dueTaskDatePicker.clear();
            }

            // SUBTASK min-max güncelle
            subStartPicker.set("minDate", selectedDates[0]);
            subDuePicker.set("minDate", selectedDates[0]);
        }
    }
});

dueTaskDatePicker = flatpickr("#txt-task-due-date", {
    altInput: true,
    altFormat: "d.m.Y",
    dateFormat: "Y-m-d",
    static: true,
    onChange(selectedDates) {
        if (selectedDates.length > 0) {
            subStartPicker.set("maxDate", selectedDates[0]);
            subDuePicker.set("maxDate", selectedDates[0]);
        }
    }
});

/* SUBTASK PICKERS */
subStartPicker = flatpickr("#txt-sub-task-start-date", {
    altInput: true,
    altFormat: "d.m.Y",
    dateFormat: "Y-m-d",
    static: true
});

subDuePicker = flatpickr("#txt-sub-task-due-date", {
    altInput: true,
    altFormat: "d.m.Y",
    dateFormat: "Y-m-d",
    static: true
});
/* -------------------------------------------------------
   TEAM MANAGEMENT – ADD / EDIT / DELETE / TABLE
------------------------------------------------------- */

/* -------------------------------------------------------
   TEAM DATATABLE INIT (CLEAN VERSION)
------------------------------------------------------- */
function initTeamDataTable(placeholderText, lanData) {

    const tableEl = document.querySelector('.workflow-team-table');
    if (!tableEl) return;

    // Eğer tablo daha önce init edildiyse sıfırla
    if ($.fn.DataTable.isDataTable(tableEl)) {
        $(tableEl).DataTable().clear().destroy();
    }

    dt_workflow_team = new DataTable(tableEl, {
        data: dummyData,
        columns: [
            { data: 'id', visible: false },
            { data: 'fullName' },
            { data: 'roles' },
            { data: 'totalHour' },
            { data: 'skills' },
            { data: null },          // Tasks
            { data: 'teamStatusName' },
            { data: null }           // Actions
        ],
        columnDefs: [
            {
                targets: 1,
                render: (data) => `<span class="fw-medium">${data}</span>`
            },
            {
                targets: 2,
                render: (roles) => {
                    if (!roles?.length) return "-";
                    return roles
                        .map(r => `<span class="badge bg-label-primary me-1">${r.name}</span>`)
                        .join("");
                }
            },
            {
                targets: 3,
                render: (data) => `${data} h`
            },
            {
                targets: 4,
                render: (data) => {
                    if (!data?.length) return "-";
                    return data
                        .map(s => `<span class="badge bg-label-secondary me-1">${s}</span>`)
                        .join("");
                }
            },
            {
                targets: 5,
                render: (_, __, row) => `
                    <span class="fw-semibold">${row.completedTask} / ${row.totalTask}</span>
                `
            },
            {
                targets: 6,
                render: (_, __, row) => {
                    const color = row.teamStatus ? "primary" : "secondary";
                    return `<span class="badge bg-label-${color}">${row.teamStatusName}</span>`;
                }
            },
            {
                targets: -1,
                orderable: false,
                render: (_, __, row) => `
                    <div class="d-flex align-items-center">
                        <a href="javascript:;" class="btn btn-icon edit-team-record" data-id="${row.id}">
                            <i class="icon-base bx bx-edit-alt icon-md"></i>
                        </a>
                        <a href="javascript:;" class="btn btn-icon delete-record" data-id="${row.id}">
                            <i class="icon-base bx bx-trash icon-md"></i>
                        </a>
                    </div>
                `
            }
        ],
        order: [[1, 'asc']],
        displayLength: 100
    });
}

/* -------------------------------------------------------
   TEAM ADD – CLEAN + RESET + VALIDATION SAFE
------------------------------------------------------- */
document.getElementById("btn-add-team-member")?.addEventListener("click", () => {

    const userId = $("#add-team-member").val();
    const fullName = $("#add-team-member option:selected").text();
    const selectedRoles = $("#ddlRoles").val() || [];
    const skillsText = $("#add-skills").val().trim();

    // Duplicate kontrol
    if (dummyData.some(m => m.userId === userId)) {
        showToast(`${fullName} already exists!`, "error");
        return;
    }

    const rolesFormatted = selectedRoles.map(id => ({
        id,
        name: $("#ddlRoles option[value='" + id + "']").text()
    }));

    const skillsArray = skillsText ? skillsText.split(",").map(s => s.trim()) : [];

    const newRecord = {
        id: Date.now(),
        userId,
        fullName,
        roles: rolesFormatted,
        skills: skillsArray,
        totalHour: 0,
        completedTask: 0,
        totalTask: 0,
        teamStatus: true,
        teamStatusName: "active"
    };

    dummyData.push(newRecord);

    // DataTable update
    $('.workflow-team-table').DataTable().clear().rows.add(dummyData).draw();

    // Reset form
    resetAddTeamForm();

    showToast("Team member added successfully!", "success");
});

/* RESET FUNCTION FOR ADD TEAM */
function resetAddTeamForm() {
    const form = document.getElementById("createTeamForm");
    form?.reset();

    $("#add-team-member").val(null).trigger("change");
    $("#ddlRoles").selectpicker("deselectAll").selectpicker("refresh");
}

/* -------------------------------------------------------
   TEAM EDIT (OPEN MODAL + FILL DATA + UPDATE)
------------------------------------------------------- */
document.addEventListener("click", function (e) {
    const btn = e.target.closest(".edit-team-record");
    if (!btn) return;

    const id = btn.getAttribute("data-id");
    const record = dummyData.find(x => x.id == id);
    if (!record) return;

    // Fill fields
    $("#update-team-member").val(record.userId).trigger("change");

    const roles = record.roles.map(r => r.id);
    $("#updateddlRoles").selectpicker("val", roles);

    $("#update-skills").val(record.skills.join(", "));

    $("#updateTeamId").val(record.id);

    $("#offcanvasUpdateTeamMember").offcanvas("show");
});

/* -------------------------------------------------------
   TEAM UPDATE
------------------------------------------------------- */
document.getElementById("btn-update-team-member")?.addEventListener("click", () => {

    const id = $("#updateTeamId").val();
    const member = dummyData.find(x => x.id == id);

    if (!member) return;

    const newUserId = $("#update-team-member").val();

    // Duplicate kontrol (kendisi hariç)
    if (dummyData.some(m => m.userId === newUserId && m.id != id)) {
        showToast("This user already exists!", "error");
        return;
    }

    member.userId = newUserId;
    member.fullName = $("#update-team-member option:selected").text();

    const rolesSelected = $("#updateddlRoles").val() || [];
    member.roles = rolesSelected.map(r => ({
        id: r,
        name: $("#updateddlRoles option[value='" + r + "']").text()
    }));

    const skillsText = $("#update-skills").val().trim();
    member.skills = skillsText ? skillsText.split(",").map(s => s.trim()) : [];

    // Update DataTable
    $('.workflow-team-table').DataTable().clear().rows.add(dummyData).draw();

    resetUpdateTeamForm();

    $("#offcanvasUpdateTeamMember").offcanvas("hide");

    showToast("Team updated successfully!", "success");
});

/* RESET UPDATE FORM */
function resetUpdateTeamForm() {
    $("#update-team-member").val(null).trigger("change");
    $("#updateddlRoles").selectpicker("deselectAll").selectpicker("refresh");
    $("#update-skills").val("");
}

/* -------------------------------------------------------
   DELETE TEAM MEMBER
------------------------------------------------------- */
function bindDeleteRecordEvent() {

    let recordIdToDelete = null;

    document.addEventListener("click", (e) => {
        const btn = e.target.closest(".delete-record");
        if (!btn) return;

        recordIdToDelete = btn.getAttribute("data-id");

        new bootstrap.Modal(document.getElementById("deleteConfirmModal")).show();
    });

    document.getElementById("confirmDeleteBtn")?.addEventListener("click", () => {

        if (!recordIdToDelete) return;

        const index = dummyData.findIndex(x => x.id == recordIdToDelete);
        if (index !== -1) {
            dummyData.splice(index, 1);

            $('.workflow-team-table').DataTable().clear().rows.add(dummyData).draw();
        }

        recordIdToDelete = null;

        bootstrap.Modal.getInstance(
            document.getElementById("deleteConfirmModal")
        ).hide();
    });
}


/* -------------------------------------------------------
   TASK / MEETING – TAB AÇILDIĞINDA YAPILACAKLAR
------------------------------------------------------- */

function onTasksTabShown() {

    // Task DataTable reset + yeniden yükle
    const tableElement = document.querySelector('.workflow-task-list-table');
    if (tableElement && $.fn.DataTable.isDataTable(tableElement)) {
        $(tableElement).DataTable().clear().destroy();
    }

    // Karakter sayaçları
    initializeCharacterCounter('add-task-name', 250);
    initializeCharacterCounter('add-task-description', 2000);

    // Kısa Task formu için selectler
    populateSelect('add-task-type', {
        apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskTypes`,
        placeholder: 'Select type',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true
    });

    populateSelect('add-task-status', {
        apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
        placeholder: 'Select status',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true
    });

    populateSelect('add-task-priority', {
        apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
        placeholder: 'Select priority',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true
    });

    // Assignee – Team dummyData üzerinden
    populateSelect('add-task-assignee', {
        data: dummyData,
        placeholder: 'Select assignee',
        valueKey: 'userId',
        textKey: 'fullName',
        autoSelectIfSingle: true
    });

    // Category
    populateSelect('add-task-category', {
        apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskCategory`,
        placeholder: 'Select category',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true
    });

    // Meeting start/end time dropdownları tek sefer doldur
    const addStart = document.querySelector("#add-meeting-start-time");
    const addEnd = document.querySelector("#add-meeting-end-time");
    if (addStart && addEnd && addStart.options.length === 0) {
        generateTimeOptions().forEach(opt => {
            addStart.add(new Option(opt.text, opt.value));
            addEnd.add(new Option(opt.text, opt.value));
        });
    }

    // Task/Meeting toggle – aynı event 2–3 kere eklenmesin
    const selectEl = document.getElementById("add-task-type");
    if (selectEl) {
        if ($(selectEl).hasClass("select2-hidden-accessible")) {
            if (!selectEl.dataset.boundChange) {
                $(selectEl).on("change", function () {
                    handleTaskMeetingToggle(this.value);
                });
                selectEl.dataset.boundChange = "true";
            }
        } else {
            if (!selectEl.dataset.boundChange) {
                selectEl.addEventListener("change", function () {
                    handleTaskMeetingToggle(this.value);
                });
                selectEl.dataset.boundChange = "true";
            }
        }
    }

    // Meeting time → ana meeting time senkronizasyonu
    $('#add-meeting-start-time').off('change').on('change', function () {
        const value = $(this).val();
        updateMainTime(value, 'start');
    });

    $('#add-meeting-end-time').off('change').on('change', function () {
        const value = $(this).val();
        updateMainTime(value, 'end');
    });

    // Dil dosyası + Task DataTable init
    const lang = localStorage.getItem('language') || 'en';
    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["Search"] || "Search";
            initWorkFlowTaskDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Language file could not be loaded:', error);
            initWorkFlowTaskDataTable("Search", null);
        });
}

document
    .querySelector('button[data-bs-target="#tasksForm"]')
    ?.addEventListener('shown.bs.tab', onTasksTabShown);

/* -------------------------------------------------------
   TASK ↔ MEETING ALAN GÖSTER / GİZLE
------------------------------------------------------- */

function handleTaskMeetingToggle(value) {
    const taskFields = document.querySelectorAll(".task-fields");
    const meetingFields = document.querySelectorAll(".meeting-fields");

    if (value === "2") {
        // Meeting
        taskFields.forEach(el => el.classList.add("d-none"));
        meetingFields.forEach(el => el.classList.remove("d-none"));
    } else {
        // Task (default)
        taskFields.forEach(el => el.classList.remove("d-none"));
        meetingFields.forEach(el => el.classList.add("d-none"));
    }
}

/* -------------------------------------------------------
   FULL FORM / NORMAL FORM GEÇİŞİ
------------------------------------------------------- */

function updateFormFields(taskTypeVal) {

    const addTaskNameEl = document.getElementById('add-task-name');
    const addTaskDescriptionEl = document.getElementById('add-task-description');
    const addTaskCategoryEl = document.getElementById('add-task-category');
    const addTaskAssigneeEl = document.getElementById('add-task-assignee');
    const addTaskStatusEl = document.getElementById('add-task-status');
    const addTaskPriorityEl = document.getElementById('add-task-priority');
    const addTaskDueDateEl = document.getElementById('add-task-due-date');
    const addTaskEstimatedHourEl = document.getElementById('add-task-estimated-hour');

    const intiativeEl = document.getElementById('txt-name');

    const taskName = addTaskNameEl?.value || '';
    const taskDescription = addTaskDescriptionEl?.value || '';
    const intiativeName = intiativeEl?.value || '';
    const taskCategory = addTaskCategoryEl?.value || '';
    const taskAssignee = addTaskAssigneeEl?.value || '';
    const taskStatus = addTaskStatusEl?.value || '';
    const taskPriority = addTaskPriorityEl?.value || '';
    const taskDueDate = addTaskDueDateEl?.value || '';
    const taskEstimatedHour = addTaskEstimatedHourEl?.value || '';

    // Alt formlar için reset
    ResetDependenciesTaskFormFields?.();
    ResetChecklistTaskFormFields?.();

    if (taskTypeVal === '1') {
        // ---- FULL TASK FORM ----
        $('#txt-task-name').val(taskName);
        $('#txt-task-description').val(taskDescription);
        $('#txt-intiatives').val(intiativeName);

        populateSelect('ddl-task-assignee', {
            data: dummyData,
            placeholder: 'Select assignee',
            valueKey: 'userId',
            textKey: 'fullName',
            selectedValue: taskAssignee
        });

        populateSelect('ddl-task-category', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskCategory`,
            placeholder: 'Select category',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: taskCategory
        });

        populateSelect('ddl-task-status', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: taskStatus
        });

        populateSelect('ddl-task-priority', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
            placeholder: 'Select priority',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: taskPriority
        });

        $('#txt-task-due-date').val(taskDueDate);
        $('#txt-task-estimated-hour').val(taskEstimatedHour);
    }

    if (taskTypeVal === '2') {
        // ---- FULL MEETING FORM ----
        $('#txt-meeting-name').val(taskName);
        $('#txt-meeting-description').val(taskDescription);
        $('#txt-meeting-intiatives').val(intiativeName);

        // Attendees main select doldurulması için pendingAttendees kullanılıyor
        const attendeesValue = $('#add-meeting-attendees').val() || [];
        pendingAttendees = attendeesValue;

        // Tarih ve saatleri ana meeting formuna taşımak için flatpickr & updateMainTime kullanılır
        const startDate = $('#add-meeting-start-date').val();
        const endDate = $('#add-meeting-end-date').val();
        const startTime = $('#add-meeting-start-time').val();
        const endTime = $('#add-meeting-end-time').val();

        if (startDate && mainStartPicker) mainStartPicker.setDate(startDate, true);
        if (endDate && mainEndPicker) mainEndPicker.setDate(endDate, true);

        if (startTime) updateMainTime(startTime, 'start');
        if (endTime) updateMainTime(endTime, 'end');
    }
}

// Full form butonu
function bindFullFormButton(fv) {
    const btnFullForm = document.getElementById('btnFullForm');
    if (!btnFullForm) return;

    btnFullForm.addEventListener('click', function () {
        const taskTypeVal = $('#add-task-type').val();

        // Task type seçili değilse validation
        if (!taskTypeVal) {
            fv.validateField('taskType');
            return;
        }

        const recordType = urlParams.get('recordType') || '';
        const taskTypeText = $('#add-task-type').find('option:selected').text() || 'Task';

        document.getElementById('hdrTask').textContent = `New ${taskTypeText}`;
        document.getElementById('pTask').textContent =
            `Add a new ${taskTypeText} to your workspace and assign it to a ${recordType} and group`;

        // Hangi sekmeler açık olacak
        handleTaskTabs?.(taskTypeVal);

        if (taskTypeVal === '1') toggleForms('taskFormContainer', 'meetingFormContainer');
        if (taskTypeVal === '2') toggleForms('meetingFormContainer', 'taskFormContainer');

        updateFormFields(taskTypeVal);

        document.getElementById('normalFormContainer').classList.add('d-none');
        document.getElementById('fullFormContainer').classList.remove('d-none');

        $('#offcanvasCreateTask').offcanvas('hide');
    });
}

document.getElementById('btnBackToNormal')?.addEventListener('click', function () {
    document.getElementById('fullFormContainer').classList.add('d-none');
    document.getElementById('normalFormContainer').classList.remove('d-none');
});

/* -------------------------------------------------------
   CREATE TASK FORM VALIDATION + SUBMIT
------------------------------------------------------- */

function initializeCreateTaskValidation() {
    const form = document.getElementById('createTask');
    if (!form) return;

    const fv = FormValidation.formValidation(form, {
        fields: {
            taskType: {
                validators: {
                    notEmpty: {
                        message: 'Task type is required'
                    }
                }
            },
            taskName: {
                validators: {
                    notEmpty: {
                        message: 'Name is required'
                    },
                    stringLength: {
                        min: 3,
                        max: 250,
                        message: 'Name must be between 3 and 250 characters'
                    },
                    callback: {
                        message: 'Name is required for the selected type',
                        callback: function (input) {
                            const type = $('#add-task-type').val();
                            if (!type) return false;
                            if (type === '1' || type === '2') return input.value.trim() !== '';
                            return true;
                        }
                    }
                }
            },
            taskDescription: {
                validators: {
                    notEmpty: {
                        message: 'Description is required'
                    },
                    stringLength: {
                        max: 2000,
                        message: 'Description cannot exceed 2000 characters'
                    },
                    callback: {
                        message: 'Description is required for the selected type',
                        callback: function (input) {
                            const type = $('#add-task-type').val();
                            if (!type) return false;
                            if (type === '1' || type === '2') return input.value.trim() !== '';
                            return true;
                        }
                    }
                }
            },
            taskAssignee: {
                validators: {
                    callback: {
                        message: 'Assignee is required for tasks',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '1' || input.value.trim() !== '';
                        }
                    }
                }
            },
            taskStatus: {
                validators: {
                    callback: {
                        message: 'Status is required for tasks',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '1' || input.value.trim() !== '';
                        }
                    }
                }
            },
            taskPriority: {
                validators: {
                    callback: {
                        message: 'Priority is required for tasks',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '1' || input.value.trim() !== '';
                        }
                    }
                }
            },
            taskDueDate: {
                validators: {
                    callback: {
                        message: 'Due Date is required for tasks',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '1' || input.value.trim() !== '';
                        }
                    }
                }
            },
            taskEstimatedHour: {
                validators: {
                    callback: {
                        message: 'Estimated hour is required for tasks',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '1' || input.value.trim() !== '';
                        }
                    }
                }
            },
            meetingAttendees: {
                validators: {
                    callback: {
                        message: 'Attendees are required for meetings',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '2' || ($(input).val() || []).length > 0;
                        }
                    }
                }
            },
            meetingStartDate: {
                validators: {
                    callback: {
                        message: 'Start Date is required for meetings',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '2' || input.value.trim() !== '';
                        }
                    }
                }
            },
            meetingStartTime: {
                validators: {
                    callback: {
                        message: 'Start Time is required for meetings',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '2' || input.value.trim() !== '';
                        }
                    }
                }
            },
            meetingEndDate: {
                validators: {
                    callback: {
                        message: 'End Date is required for meetings',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '2' || input.value.trim() !== '';
                        }
                    }
                }
            },
            meetingEndTime: {
                validators: {
                    callback: {
                        message: 'End Time is required for meetings',
                        callback: function (input) {
                            return $('#add-task-type').val() !== '2' || input.value.trim() !== '';
                        }
                    }
                }
            }
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: '',
                rowSelector: function () {
                    return '.form-control-validation';
                }
            }),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });

    // Full form butonu validation’a bağlı
    bindFullFormButton(fv);

    const btnCreate = document.getElementById('btnCreateTask');
    if (btnCreate) {
        btnCreate.addEventListener('click', function () {
            fv.validate().then(function (status) {
                if (status === 'Valid') {
                    console.log('Task form valid, submit edilebilir.');
                    // Buraya backend POST işlemini ekleyebilirsin
                } else {
                    console.log('Task form invalid, lütfen hataları düzeltin.');
                }
            });
        });
    }
}
document.addEventListener("DOMContentLoaded", () => {

    const pathParts = window.location.pathname.split("/");
    const workflowId = window.location.pathname.split('/')[2];

    // Eğer workflowId "blank" değilse → güncelleme modu
    if (workflowId && workflowId !== "blank") {
        loadWorkflow(workflowId);
    }

    initializeCreateTaskValidation();
});
/* -------------------------------------------------------
   4. PARÇA – SUB TASK / DEPENDENCIES / CHECKLIST / AGENDA
------------------------------------------------------- */

/* =======================================================
   =========     SUB TASK MANAGEMENT     =================
======================================================= */

let subTasks = []; // Global array

function resetSubTaskForm() {
    $("#txt-sub-task-name").val("");
    $("#txt-sub-task-description").val("");
    $("#txt-sub-task-start-date").val("");
    $("#txt-sub-task-due-date").val("");
    $("#txt-sub-task-estimated-hour").val("");
    subStartPicker?.clear();
    subDuePicker?.clear();
}

function addSubTask() {
    const name = $("#txt-sub-task-name").val().trim();
    const description = $("#txt-sub-task-description").val().trim();
    const startDate = $("#txt-sub-task-start-date").val();
    const dueDate = $("#txt-sub-task-due-date").val();
    const hour = $("#txt-sub-task-estimated-hour").val();

    if (!name || !description || !startDate || !dueDate) {
        showToast("Please fill all required fields!", "error");
        return;
    }

    subTasks.push({
        id: Date.now(),
        name,
        description,
        startDate,
        dueDate,
        hour
    });

    renderSubTaskList();
    resetSubTaskForm();
}

function deleteSubTask(id) {
    subTasks = subTasks.filter(s => s.id !== id);
    renderSubTaskList();
}

function renderSubTaskList() {
    const container = document.getElementById("subTaskList");
    if (!container) return;

    if (subTasks.length === 0) {
        container.innerHTML = `<p class="text-muted">No sub tasks added.</p>`;
        return;
    }

    container.innerHTML = subTasks
        .map(s => `
            <div class="border rounded p-3 mb-2 d-flex justify-content-between align-items-center">
                <div>
                    <strong>${s.name}</strong>
                    <div class="text-muted small">
                        ${s.startDate} → ${s.dueDate}
                    </div>
                </div>
                <button class="btn btn-sm btn-danger" onclick="deleteSubTask(${s.id})">
                    Remove
                </button>
            </div>
        `)
        .join("");
}


/* =======================================================
   =========     DEPENDENCIES MANAGEMENT     =============
======================================================= */

let dependenciesTasks = [];

function resetDependenciesTaskFormFields() {
    dependenciesTasks = [];
    $("#ddl-dependencies-tasks").val(null).trigger("change");
    $("#dependencyTaskList").html(`<p class="text-muted">No dependencies added.</p>`);
}

function addDependencyTask() {
    const selected = $("#ddl-dependencies-tasks").val();
    if (!selected) {
        showToast("Select a task to add.", "error");
        return;
    }

    const exists = dependenciesTasks.includes(selected);
    if (exists) {
        showToast("This dependency already exists.", "error");
        return;
    }

    const taskText = $("#ddl-dependencies-tasks option:selected").text();
    dependenciesTasks.push(selected);

    renderDependencyList();
}

function renderDependencyList() {
    const container = document.getElementById("dependencyTaskList");
    if (!container) return;

    if (dependenciesTasks.length === 0) {
        container.innerHTML = `<p class="text-muted">No dependencies added.</p>`;
        return;
    }

    container.innerHTML = dependenciesTasks
        .map(id => {
            const name = $("#ddl-dependencies-tasks option[value='" + id + "']").text();
            return `
                <div class="border rounded p-2 mb-2 d-flex justify-content-between align-items-center">
                    <span>${name}</span>
                    <button class="btn btn-sm btn-danger" onclick="removeDependency('${id}')">Remove</button>
                </div>`;
        })
        .join("");
}

function removeDependency(id) {
    dependenciesTasks = dependenciesTasks.filter(x => x !== id);
    renderDependencyList();
}


/* =======================================================
   =========     CHECKLIST MANAGEMENT       ==============
======================================================= */

let checklistTasks = [];

function resetChecklistTaskFormFields() {
    checklistTasks = [];
    $("#txt-checklist-item").val("");
    renderChecklist();
}

function addChecklistItem() {
    const text = $("#txt-checklist-item").val().trim();
    if (!text) {
        showToast("Checklist item cannot be empty.", "error");
        return;
    }

    checklistTasks.push({
        id: Date.now(),
        text: text,
        done: false
    });

    $("#txt-checklist-item").val("");
    renderChecklist();
}

function toggleChecklist(id) {
    const item = checklistTasks.find(x => x.id === id);
    if (item) {
        item.done = !item.done;
    }
    renderChecklist();
}

function deleteChecklist(id) {
    checklistTasks = checklistTasks.filter(x => x.id !== id);
    renderChecklist();
}

function renderChecklist() {
    const container = document.getElementById("checklistContainer");
    if (!container) return;

    if (checklistTasks.length === 0) {
        container.innerHTML = `<p class="text-muted">No checklist items added.</p>`;
        return;
    }

    container.innerHTML = checklistTasks
        .map(item => `
            <div class="d-flex align-items-center p-2 border rounded mb-2">
                <input type="checkbox" class="me-2" ${item.done ? "checked" : ""}
                    onclick="toggleChecklist(${item.id})">
                <span class="${item.done ? "text-decoration-line-through" : ""}">
                    ${item.text}
                </span>
                <button class="btn btn-sm btn-danger ms-auto"
                    onclick="deleteChecklist(${item.id})">
                    Remove
                </button>
            </div>
        `)
        .join("");
}


/* =======================================================
   =========         AGENDA MANAGEMENT        ============
======================================================= */

let agendaCounter = 0;

function resetAgendaItems() {
    agendaCounter = 0;
    document.getElementById("agendaDetail").innerHTML = "";
}

/* Agenda item template clone + yönetimi */
function addAgendaItem() {

    agendaCounter++;

    const container = document.getElementById("agendaDetail");
    const template = document.getElementById("agenda-item-template");

    const newItem = template.cloneNode(true);
    newItem.classList.remove("d-none");
    newItem.removeAttribute("id");
    container.appendChild(newItem);

    // Numara
    newItem.querySelector(".agenda-number").textContent = agendaCounter;

    // Element referansları
    const startDateInput = newItem.querySelector(".agenda-start-date");
    const endDateInput = newItem.querySelector(".agenda-end-date");
    const startTimeSelect = newItem.querySelector(".agenda-start-time");
    const endTimeSelect = newItem.querySelector(".agenda-end-time");
    const attendeesSelect = newItem.querySelector(".agenda-attendees");

    // Main meeting değerleri
    const meetingStartDate = mainStartPicker.selectedDates[0];
    const meetingEndDate = mainEndPicker.selectedDates[0];
    const meetingStartTime = $("#txt-meeting-start-time").val();
    const meetingEndTime = $("#txt-meeting-end-time").val();

    // Flatpickr bağla
    const agendaStartFp = flatpickr(startDateInput, {
        altInput: true,
        altFormat: "d.m.Y",
        dateFormat: "Y-m-d",
        minDate: meetingStartDate,
        maxDate: meetingEndDate,
        onChange: function (dates) {
            if (dates.length > 0) {
                agendaEndFp.set("minDate", dates[0]);
            }
        }
    });

    const agendaEndFp = flatpickr(endDateInput, {
        altInput: true,
        altFormat: "d.m.Y",
        dateFormat: "Y-m-d",
        minDate: meetingStartDate,
        maxDate: meetingEndDate
    });

    // Saat dropdownları
    generateTimeOptions().forEach(opt => {
        startTimeSelect.add(new Option(opt.text, opt.value));
        endTimeSelect.add(new Option(opt.text, opt.value));
    });

    // Ana meeting saatlerini default yap
    if (meetingStartTime) startTimeSelect.value = meetingStartTime;
    if (meetingEndTime) endTimeSelect.value = meetingEndTime;

    // Attendees selecti doldur
    $(attendeesSelect).html(
        dummyData
            .map(u => `<option value="${u.userId}">${u.fullName}</option>`)
            .join("")
    );

    $(attendeesSelect).selectpicker("refresh");
}

function buildTaskObject() {

    const typeId = $("#add-task-type").val();  // 1 = Task, 2 = Meeting
    const name = $("#add-task-name").val();
    const description = $("#add-task-description").val();
    const category = $("#add-task-category").val();
    const assignee = $("#add-task-assignee").val();
    const status = $("#add-task-status").val();
    const priority = $("#add-task-priority").val();
    const dueDate = $("#add-task-due-date").val();
    const estimatedHour = $("#add-task-estimated-hour").val();

    const intiativeName = $("#txt-name").val() || "";

    let obj = {
        id: Date.now(),
        typeId,
        typeName: typeId === "2" ? "Meeting" : "Task",
        name,
        description,
        intiativeName,
        categoryId: category,
        categoryName: $("#add-task-category option:selected").text(),
        assigneeId: assignee,
        ownerName: $("#add-task-assignee option:selected").text(),
        statusId: status,
        statusName: $("#add-task-status option:selected").text(),
        priorityId: priority,
        priorityName: $("#add-task-priority option:selected").text(),
        dueDate,
        estimatedHour,
        progress: 0,
        completedHour: 0,
        subTasks,
        dependenciesTasks,
        checklistTasks
    };

    if (typeId === "1") {
        return obj;
    }

    // ------------------------------
    // MEETING KAYIT NESNESİ
    // ------------------------------

    const attendees = $("#add-meeting-attendees").val() || [];
    const startDate = $("#add-meeting-start-date").val();
    const endDate = $("#add-meeting-end-date").val();
    const startTime = $("#add-meeting-start-time").val();
    const endTime = $("#add-meeting-end-time").val();

    obj.attendees = attendees;
    obj.attendeesNames = attendees.map(id =>
        $("#add-meeting-attendees option[value='" + id + "']").text()
    );

    obj.startDateTime = `${startDate}T${startTime}`;
    obj.endDateTime = `${endDate}T${endTime}`;

    return obj;
}


document.getElementById("btnCreateTaskQuick")?.addEventListener("click", function () {

    const type = $("#add-task-type").val();
    if (!type) {
        showToast("Please select task type.", "error");
        return;
    }

    const newTask = buildTaskObject();

    // Dummy listeye ekle
    dummyTaskData.push(newTask);

    // DataTable yenile
    $(".workflow-task-list-table").DataTable().clear().rows.add(dummyTaskData).draw();

    // Reset
    resetTaskQuickForm();
    resetDependenciesTaskFormFields();
    resetChecklistTaskFormFields();
    resetSubTaskForm();

    showToast("Task added successfully!", "success");
});
document.getElementById("btnCreateTaskFull")?.addEventListener("click", function () {

    const taskType = $("#add-task-type").val();

    // Normal formdan alınanlar + full formdan alınanları merge ediyoruz
    const full = buildTaskObject();

    if (taskType === "1") {
        full.name = $("#txt-task-name").val();
        full.description = $("#txt-task-description").val();
        full.categoryId = $("#ddl-task-category").val();
        full.categoryName = $("#ddl-task-category option:selected").text();
        full.assigneeId = $("#ddl-task-assignee").val();
        full.ownerName = $("#ddl-task-assignee option:selected").text();
        full.statusId = $("#ddl-task-status").val();
        full.statusName = $("#ddl-task-status option:selected").text();
        full.priorityId = $("#ddl-task-priority").val();
        full.priorityName = $("#ddl-task-priority option:selected").text();
        full.dueDate = $("#txt-task-due-date").val();
        full.estimatedHour = $("#txt-task-estimated-hour").val();
    }

    if (taskType === "2") {
        full.name = $("#txt-meeting-name").val();
        full.description = $("#txt-meeting-description").val();
        full.attendees = $("#ddl-meeting-attendees").val() || [];
        full.attendeesNames = full.attendees.map(id =>
            $("#ddl-meeting-attendees option[value='" + id + "']").text()
        );
        full.startDateTime = mainStartPicker.input.value;
        full.endDateTime = mainEndPicker.input.value;
    }

    dummyTaskData.push(full);

    $(".workflow-task-list-table").DataTable().clear().rows.add(dummyTaskData).draw();

    // Reset everything
    resetTaskQuickForm();
    resetDependenciesTaskFormFields();
    resetChecklistTaskFormFields();
    resetSubTaskForm();
    $("#fullFormContainer").addClass("d-none");
    $("#normalFormContainer").removeClass("d-none");

    showToast("Task created successfully!", "success");
});
document.addEventListener("click", function (e) {

    const btn = e.target.closest(".edit-task-record");
    if (!btn) return;

    const id = btn.getAttribute("data-id");
    const record = dummyTaskData.find(x => x.id == id);
    if (!record) return;

    // Form doldur
    $("#edit-task-id").val(record.id);
    $("#edit-task-name").val(record.name);
    $("#edit-task-description").val(record.description);
    $("#edit-task-category").val(record.categoryId).trigger("change");
    $("#edit-task-assignee").val(record.assigneeId).trigger("change");
    $("#edit-task-status").val(record.statusId).trigger("change");
    $("#edit-task-priority").val(record.priorityId).trigger("change");
    $("#edit-task-due-date").val(record.dueDate);

    flatpickr("#edit-task-due-date")?.setDate(record.dueDate, true);

    $("#offcanvasEditTask").offcanvas("show");
});
document.getElementById("btnUpdateTask")?.addEventListener("click", function () {

    const id = $("#edit-task-id").val();
    const task = dummyTaskData.find(x => x.id == id);
    if (!task) return;

    task.name = $("#edit-task-name").val();
    task.description = $("#edit-task-description").val();
    task.categoryId = $("#edit-task-category").val();
    task.categoryName = $("#edit-task-category option:selected").text();
    task.assigneeId = $("#edit-task-assignee").val();
    task.ownerName = $("#edit-task-assignee option:selected").text();
    task.statusId = $("#edit-task-status").val();
    task.statusName = $("#edit-task-status option:selected").text();
    task.priorityId = $("#edit-task-priority").val();
    task.priorityName = $("#edit-task-priority option:selected").text();
    task.dueDate = $("#edit-task-due-date").val();

    $(".workflow-task-list-table").DataTable().clear().rows.add(dummyTaskData).draw();

    $("#offcanvasEditTask").offcanvas("hide");
    showToast("Task updated successfully!", "success");
});
function bindDeleteTaskRecordEvent() {

    let toDeleteId = null;

    document.addEventListener("click", (e) => {
        const btn = e.target.closest(".delete-task-record");
        if (!btn) return;

        toDeleteId = btn.getAttribute("data-id");
        new bootstrap.Modal(document.getElementById("deleteConfirmModal")).show();
    });

    document.getElementById("confirmDeleteBtn")?.addEventListener("click", () => {

        if (!toDeleteId) return;

        dummyTaskData = dummyTaskData.filter(x => x.id != toDeleteId);

        $(".workflow-task-list-table").DataTable().clear().rows.add(dummyTaskData).draw();

        toDeleteId = null;

        bootstrap.Modal.getInstance(document.getElementById("deleteConfirmModal")).hide();
    });
}

bindDeleteTaskRecordEvent();

function buildWorkflowObject() {

    const workflowId = window.location.pathname.split('/')[2];
    const recordTypeId = urlParams.get('recordTypeId') || '';
    const categoryId = urlParams.get('categoryId') || '';
    const userName = window.getUserName();

    return {
        id: workflowId === "blank" ? null : workflowId,
        name: sanitizeInput($("#txt-name").val(), { maxLength: 250 }),
        description: sanitizeInput($("#txt-description").val(), { maxLength: 2000 }),
        idCode: sanitizeInput($("#txt-code").val(), { maxLength: 50 }),
        recordTypeId,
        workflowCategoryId: categoryId,
        startDate: new Date().toISOString(),
        endDate: new Date().toISOString(),
        workflowStatusId: $("#ddl-status").val(),
        priorityId: $("#ddl-priority").val(),
        workFlowTeams: dummyData || [],
        workFlowTasks: dummyTaskData || [],
        createdBy: userName
    };
}
function validateWorkflowForm() {

    if (!$("#txt-name").val().trim()) {
        showToast("Workflow name is required", "error");
        return false;
    }
    if (!$("#txt-code").val().trim()) {
        showToast("Workflow code is required", "error");
        return false;
    }
    if (!$("#ddl-status").val()) {
        showToast("Workflow status is required", "error");
        return false;
    }
    if (!$("#ddl-priority").val()) {
        showToast("Workflow priority is required", "error");
        return false;
    }

    return true;
}
async function createWorkflow(data) {

    try {
        const response = await fetch(`${window.ApiBaseUrl}/services/DitenPPM/Workflow/CreateWorkflow`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });

        const result = await response.json();

        if (!result?.success) {
            showToast(result?.message || "Workflow could not be created.", "error");
            return;
        }

        showToast("Workflow created successfully!", "success");
        setTimeout(() => window.location.href = "/ppm/workflow-overview", 800);

    } catch (err) {
        console.error(err);
        showToast("Unexpected server error!", "error");
    }
}
async function updateWorkflow(data) {

    try {
        const response = await fetch(`${window.ApiBaseUrl}/services/DitenPPM/Workflow/UpdateWorkflow`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });

        const result = await response.json();

        if (!result?.success) {
            showToast(result?.message || "Workflow could not be updated.", "error");
            return;
        }

        showToast("Workflow updated successfully!", "success");
        setTimeout(() => window.location.href = "/ppm/workflow-overview", 800);

    } catch (err) {
        console.error(err);
        showToast("Unexpected server error!", "error");
    }
}
document.getElementById("btnCreate")?.addEventListener("click", async function () {

    // 1) Validate UI
    if (!validateWorkflowForm()) return;

    // 2) Final JSON oluştur
    const workflow = buildWorkflowObject();

    // 3) Kaydet / Güncelle seçimi
    if (!workflow.id) {
        // blank → create
        await createWorkflow(workflow);
    } else {
        // update
        await updateWorkflow(workflow);
    }
});
function resetWorkflowFormFields() {
    $("#txt-name").val("");
    $("#txt-code").val("");
    $("#txt-description").val("");
    $("#ddl-status").val(null).trigger("change");
    $("#ddl-priority").val(null).trigger("change");

    dummyData = [];
    dummyTaskData = [];

    $(".workflow-team-table").DataTable().clear().draw();
    $(".workflow-task-list-table").DataTable().clear().draw();
}
function resetAllFormsUI() {

    // TEAM
    $("#add-team-member").val(null).trigger("change");
    $("#ddlRoles").selectpicker("deselectAll").selectpicker("refresh");

    $("#update-team-member").val(null).trigger("change");
    $("#updateddlRoles").selectpicker("deselectAll").selectpicker("refresh");
    $("#update-skills").val("");

    // TASK (Quick)
    $("#add-task-name").val("");
    $("#add-task-description").val("");
    $("#add-task-estimated-hour").val("");
    $("#add-task-due-date").val("");
    $("#add-task-type").val(null).trigger("change");
    $("#add-task-status").val(null).trigger("change");
    $("#add-task-priority").val(null).trigger("change");
    $("#add-task-assignee").val(null).trigger("change");
    $("#add-task-category").val(null).trigger("change");

    // TASK FULL
    $("#txt-task-name").val("");
    $("#txt-task-description").val("");
    $("#txt-task-intiatives").val("");
    $("#ddl-task-category").val(null).trigger("change");
    $("#ddl-task-assignee").val(null).trigger("change");
    $("#ddl-task-status").val(null).trigger("change");
    $("#ddl-task-priority").val(null).trigger("change");
    $("#txt-task-due-date").val("");
    $("#txt-task-estimated-hour").val("");

    // MEETING (Quick)
    $("#add-meeting-attendees").val([]).trigger("change");
    $("#add-meeting-start-date").val("");
    $("#add-meeting-end-date").val("");
    $("#add-meeting-start-time").val("").trigger("change");
    $("#add-meeting-end-time").val("").trigger("change");

    // MEETING FULL
    $("#txt-meeting-name").val("");
    $("#txt-meeting-description").val("");
    $("#txt-meeting-intiatives").val("");
    $("#ddl-meeting-attendees").val([]).trigger("change");
    $("#txt-meeting-start-time").val("");
    $("#txt-meeting-end-time").val("");

    // FLATPICKR CLEAR
    document.querySelectorAll(".flatpickr-input").forEach(el => {
        if (el._flatpickr) {
            el._flatpickr.clear();
        }
    });
}
document.querySelectorAll(".offcanvas").forEach(off => {
    off.addEventListener("hidden.bs.offcanvas", () => {
        resetAllFormsUI();
        resetSubTaskForm();
        resetChecklistTaskFormFields();
        resetDependenciesTaskFormFields();
        resetAgendaItems();
    });
});
$(document).on('select2:close', 'select', function () {
    $(this).removeClass("is-invalid");
});
function refreshAllSelectPickers() {
    $('.selectpicker').each(function () {
        try { $(this).selectpicker('refresh'); } catch { }
    });
}
function safeUpdateFlatpickr(fp, field, value) {
    if (fp && fp.config && fp.config[field] !== value) {
        fp.set(field, value);
    }
}
document
    .querySelector('button[data-bs-target="#tasksForm"]')
    ?.addEventListener("shown.bs.tab", function () {

        refreshAllSelectPickers();

        // Splitter animasyonu glitch fix
        setTimeout(() => {
            document.querySelectorAll('.dataTables_wrapper').forEach(el => {
                el.style.opacity = 1;
            });
        }, 150);
    });
function toggleForms(showId, hideId) {
    const show = document.getElementById(showId);
    const hide = document.getElementById(hideId);

    hide.classList.add("d-none");
    show.classList.remove("d-none");

    // Smooth UI saftransition
    show.style.opacity = "0";
    setTimeout(() => { show.style.opacity = "1"; }, 50);
}
const uiCache = {
    teamTable: document.querySelector(".workflow-team-table"),
    taskTable: document.querySelector(".workflow-task-list-table"),
    agendaContainer: document.getElementById("agendaDetail"),
    offcanvasTask: document.getElementById("offcanvasCreateTask")
};
function showToast(msg, type = "success") {
    const icon = type === "error" ? "bx bx-error" : "bx bx-check-circle";

    Toastify({
        text: msg,
        gravity: "top",
        position: "right",
        duration: 3000,
        close: true,
        escapeMarkup: false,
        className:
            type === "error"
                ? "bg-danger text-white"
                : "bg-success text-white",
        avatar: `<i class="${icon} text-white"></i>`
    }).showToast();
}
function resetGlobalWorkflowState() {
    dummyData = [];
    dummyTaskData = [];
    subTasks = [];
    dependenciesTasks = [];
    checklistTasks = [];
    pendingAttendees = [];
    pendingAddToMainStart = null;
    pendingAddToMainEnd = null;
    agendaCounter = 0;
}
$("#add-task-name").on("input", function () {

    const text = $(this).val().trim();
    if (text.length < 3) return;

    // Suggest description
    if (!$("#add-task-description").val()) {
        $("#add-task-description").val(`Details about "${text}" task...`);
    }

    // Suggest category (ilk kategori seçili değilse)
    const categorySelect = $("#add-task-category");
    if (!categorySelect.val()) {
        const firstOpt = categorySelect.find("option:eq(1)").val();
        if (firstOpt) categorySelect.val(firstOpt).trigger("change");
    }

    // Suggest assignee (team lead varsa)
    if (!$("#add-task-assignee").val()) {
        const teamLead = dummyData.find(m => m.roles.some(r => r.name.includes("Lead")));
        if (teamLead) {
            $("#add-task-assignee").val(teamLead.userId).trigger("change");
        }
    }
});
function saveDraft() {
    const draft = {
        name: $("#add-task-name").val(),
        description: $("#add-task-description").val(),
        priority: $("#add-task-priority").val(),
        due: $("#add-task-due-date").val()
    };
    localStorage.setItem("draft_add_task", JSON.stringify(draft));
}

function loadDraft() {
    const draft = JSON.parse(localStorage.getItem("draft_add_task"));
    if (!draft) return;

    $("#add-task-name").val(draft.name);
    $("#add-task-description").val(draft.description);
    $("#add-task-priority").val(draft.priority).trigger("change");
    $("#add-task-due-date").val(draft.due);
}

document.getElementById("offcanvasCreateTask")?.addEventListener("shown.bs.offcanvas", loadDraft);
document.getElementById("offcanvasCreateTask")?.addEventListener("input", saveDraft);
$(document).on("dblclick", ".task-desc", function () {

    const td = $(this);
    const oldValue = td.text().trim();

    const input = $(`<input type="text" class="form-control form-control-sm" value="${oldValue}"/>`);
    td.html(input);

    input.focus();

    input.on("blur keydown", function (e) {
        if (e.type === "blur" || e.key === "Enter") {
            const newValue = input.val().trim();
            td.text(newValue);

            const rowId = td.closest("tr").find(".delete-task-record").data("id");
            const record = dummyTaskData.find(t => t.id == rowId);

            if (record) {
                record.description = newValue;
            }
        }
    });
});
function enableTaskTableDragDrop() {

    const table = document.querySelector(".workflow-task-list-table tbody");
    if (!table) return;

    let draggingRow = null;

    table.querySelectorAll("tr").forEach(row => {

        row.draggable = true;

        row.addEventListener("dragstart", (e) => {
            draggingRow = row;
            row.classList.add("dragging");
            e.dataTransfer.effectAllowed = "move";
        });

        row.addEventListener("dragover", (e) => {
            e.preventDefault();

            const targetRow = e.target.closest("tr");

            if (!targetRow || targetRow === draggingRow) return;

            const bounding = targetRow.getBoundingClientRect();
            const offset = bounding.y + bounding.height / 2;

            if (e.clientY - offset > 0) {
                targetRow.after(draggingRow);
            } else {
                targetRow.before(draggingRow);
            }
        });

        row.addEventListener("dragend", () => {
            row.classList.remove("dragging");
            updateTaskOrder();
        });
    });
}

function initWorkFlowTaskDataTable(placeholderText, lanData) {

    dt_workflow_task_table = document.querySelector('.workflow-task-list-table');
    if (!dt_workflow_task_table) return;

    dt_workflow_task = new DataTable(dt_workflow_task_table, {
        data: dummyTaskData,

        columns: [
            { data: 'id', visible: false },
            { data: 'name' },
            { data: 'typeName' },
            { data: 'categoryName' },
            { data: 'ownerName' },
            { data: 'priorityName' },
            { data: 'progress' },
            {
                data: null,
                render: (data, type, row) => {
                    const start = new Date(row.startDateTime);
                    const end = new Date(row.endDateTime);
                    const formatDate = (d) =>
                        `${String(d.getDate()).padStart(2, '0')}.${String(d.getMonth() + 1).padStart(2, '0')}.${d.getFullYear()}`;
                    return `
                        <div class="d-flex flex-column">
                            <small>S: ${formatDate(start)}</small>
                            <small>E: ${formatDate(end)}</small>
                        </div>
                    `;
                }
            },
            {
                data: null,
                render: (data, type, row) => `${row.completedHour}/${row.estimatedHour}`
            },
            { data: 'statusName' },
            { data: null }
        ],

        columnDefs: [
            // ... (senin tüm columnDefs kısımların değişmeden aynı kalıyor)
        ],

        ordering: false, // Sıralamayı kapattık çünkü drag-drop ile yönetiyoruz
        displayLength: 100,

        layout: {
            // ... (senin layout ayarların olduğu gibi durur)
        },

        language: {
            // ... (senin language ayarların)
        },

        responsive: {
            // ... (senin responsive ayarların)
        },

        initComplete: function () {
            modifyDataTableLayout();
            enableTaskTableDragDrop();   // İlk yüklemede aktif
        },

        drawCallback: function () {
            modifyDataTableLayout();
            enableTaskTableDragDrop();   // Her çizimde tekrar aktif
        }
    });

}

async function updateTaskOrder() {

    const dt = $(".workflow-task-list-table").DataTable();

    // DOM sırasına göre güncelle
    document.querySelectorAll(".workflow-task-list-table tbody tr")
        .forEach((row, index) => {

            const rowData = dt.row(row).data();
            const task = dummyTaskData.find(x => x.id === rowData.id);

            if (task) {
                task.sortOrder = index + 1;
            }
        });

    showToast("Task order updated.", "success");
    // Backend'e kaydet
    await saveTaskOrderToBackend();
}

async function saveTaskOrderToBackend() {

    const workflowId = window.location.pathname.split("/")[2];

    const payload = {
        workflowId: workflowId,
        tasks: dummyTaskData.map(t => ({
            taskId: t.id,
            sortOrder: t.sortOrder
        }))
    };

    try {
        const response = await fetch(
            `${window.ApiBaseUrl}/services/DitenPPM/Workflow/UpdateTaskOrder`,
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            }
        );

        const result = await response.json();

        if (!result?.success) {
            console.warn("Task order API error:", result?.message);
            showToast("Sort order saved locally, but not updated on server.", "error");
            return;
        }

        showToast("Task order saved!", "success");

    } catch (err) {
        console.error("Task order save error:", err);
        showToast("Task order saved locally, but server unreachable.", "error");
    }
}


$("#add-meeting-attendees").on("keyup", function () {

    const text = $(this).val().toLowerCase();

    const filtered = dummyData.filter(m =>
        m.fullName.toLowerCase().includes(text)
    );

    $("#attendeeSuggestions").html(
        filtered.map(m => `
            <div class="p-2 hover-bg-light cursor-pointer"
                 onclick="selectAttendee('${m.userId}')">
                 ${m.fullName}
            </div>
        `).join("")
    );
});

function selectAttendee(id) {
    const attendees = $("#add-meeting-attendees").val() || [];
    if (!attendees.includes(id)) {
        attendees.push(id);
        $("#add-meeting-attendees").val(attendees).trigger("change");
    }
    $("#attendeeSuggestions").empty();
}
let isDirty = false;

$("input, select, textarea").on("change input", () => {
    isDirty = true;
});

window.addEventListener("beforeunload", function (e) {
    if (!isDirty) return;
    e.preventDefault();
    e.returnValue = "";
});
function markAsClean() {
    isDirty = false;
}
document.addEventListener("keydown", function (e) {

    // Save workflow
    if (e.ctrlKey && e.key.toLowerCase() === "s") {
        e.preventDefault();
        document.getElementById("btnCreate").click();
    }

    // Close offcanvas (ESC)
    if (e.key === "Escape") {
        const activeCanvas = document.querySelector(".offcanvas.show");
        if (activeCanvas) {
            bootstrap.Offcanvas.getInstance(activeCanvas).hide();
        }
    }
});
/* -------------------------------------------------------
   SECURITY HELPERS – XSS ve Input Sanitization
------------------------------------------------------- */

function escapeHtml(value) {
    if (value == null) return "";
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

// Formlardan gelen inputları normalize etmek için:
function sanitizeInput(value, options = {}) {
    if (value == null) return "";
    let v = String(value).trim();

    // Script tag vs. basic temizleme
    v = v.replace(/<\s*script/gi, "");
    v = v.replace(/javascript:/gi, "");

    // İstersen sadece belli uzunluğa kadar kes:
    if (options.maxLength && v.length > options.maxLength) {
        v = v.substring(0, options.maxLength);
    }

    return v;
}








