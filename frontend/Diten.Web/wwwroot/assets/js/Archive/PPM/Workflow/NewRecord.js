'use strict';
const port2 = protocol === 'https:' ? '5055' : '5050';
const userName = window.getUserName();
let dt_workflow_team_table;
let dt_workflow_team;
let dt_workflow_task_table;
let dt_workflow_task;
const urlParams = new URLSearchParams(window.location.search);
const startDateEl = document.querySelector('#add-meeting-start-date');
const endDateEl = document.querySelector('#add-meeting-end-date');
const dueDateEl = document.querySelector('#add-task-due-date');
const startTaskDateEl = document.querySelector('#dtTaskStartDate');
const dueTaskDateEl = document.querySelector('#dtTaskDueDate');
// --- MAIN FORM PICKERS ---
let startPicker, endPicker;

startPicker = flatpickr("#dtStartDate", {
    dateFormat: "d.m.Y",   // ✔ dd.MM.yyyy formatı
    allowInput: true,
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

endPicker = flatpickr("#dtEndDate", {
    dateFormat: "d.m.Y",   // ✔ dd.MM.yyyy formatı
    allowInput: true,
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

const mainStartEl = document.querySelector('#txt-meeting-start-date');
const mainEndEl = document.querySelector('#txt-meeting-end-date');
let fvSub = null;
let startDatePicker, endDatePicker;
let mainStartPicker, mainEndPicker;
startDatePicker = flatpickr(startDateEl, {
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    onChange: function (selectedDates) {
        if (selectedDates.length > 0) {
            const startDate = selectedDates[0];
            // End Date picker minimum date olarak Start Date'i alır
            if (endDatePicker) endDatePicker.set('minDate', startDate);
            // Main start set
            mainStartPicker.setDate(startDate, true);

            // Main end minDate
            mainEndPicker.set('minDate', startDate);
        }
    }
});
endDatePicker = flatpickr(endDateEl, {
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    onChange: function (selectedDates) {
        if (selectedDates.length > 0) {
            const endDate = selectedDates[0];

            // START picker maxDate olarak endDate'i ayarla
            startDatePicker.set('maxDate', endDate);

            // Main end set
            mainEndPicker.setDate(endDate, true);

            // Main start maxDate
            mainStartPicker.set('maxDate', endDate);
        }
    }
});

// --- MAIN FORM PICKERS ---
mainStartPicker = flatpickr(mainStartEl, {
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    onChange: function (selectedDates) {
        if (selectedDates.length > 0) {
            const startDate = selectedDates[0];

            // Main end minDate
            mainEndPicker.set('minDate', startDate);
         
        }
    }
});
mainEndPicker = flatpickr(mainEndEl, {
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    onChange: function (selectedDates) {
        if (selectedDates.length > 0) {
            const endDate = selectedDates[0];

            // Main start maxDate
            mainStartPicker.set('maxDate', endDate);
        }
    }
});


const dueDatePicker = flatpickr(dueDateEl, {
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    minDate: 'today', // minimum tarih bugün
    onChange: function (selectedDates) {
        if (selectedDates.length > 0) {
            // add-task-due-date seçildiğinde dtTaskDueDate'e aktar
            dueTaskDatePicker.setDate(selectedDates[0], true);
        }
    }
});


let isStartChanged = false;
let isDueChanged = false;

const startTaskDatePicker = flatpickr(startTaskDateEl, {
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    onChange: function (selectedDates) {
        if (selectedDates.length > 0) {
            isStartChanged = true;

            // Start seçildiyse Due minimum start olmalı
            dueTaskDatePicker.set('minDate', selectedDates[0]);

            // Eğer Due daha önce seçilmişse, Start > Due olamaz → Due'u temizle
            if (isDueChanged) {
                const dueValue = dueTaskDatePicker.selectedDates[0];
                if (dueValue && dueValue < selectedDates[0]) {
                    dueTaskDatePicker.clear();
                }
            }

            // --- Sub Task min/max güncelle
            subStartPicker.set('minDate', selectedDates[0]);
            subStartPicker.set('maxDate', dueTaskDatePicker.selectedDates[0] || dueTaskDateEl.value);
            subDuePicker.set('minDate', selectedDates[0]);

        }
    }
});

let dueTaskDatePicker = flatpickr(dueTaskDateEl, {
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    onChange: function (selectedDates) {
        if (selectedDates.length > 0) {
            isDueChanged = true;

            // Due seçildiyse Start maksimum due olabilir
            startTaskDatePicker.set('maxDate', selectedDates[0]);

            // Eğer Start daha önce seçilmişse, Start > Due olamaz → Start'ı temizle
            if (isStartChanged) {
                const startValue = startTaskDatePicker.selectedDates[0];
                if (startValue && startValue > selectedDates[0]) {
                    startTaskDatePicker.clear();
                }
            }

            // --- Sub Task min/max güncelle
            subStartPicker.set('maxDate', selectedDates[0]);
            subDuePicker.set('maxDate', selectedDates[0]);
            subDuePicker.set('minDate', startTaskDatePicker.selectedDates[0] || startTaskDateEl.value);
        }
    }
});

// --- Sub Task pickers ---
const subStartEl = document.querySelector('#dtSubTaskStartDate');
const subDueEl = document.querySelector('#dtSubTaskDueDate');

const subStartPicker = flatpickr(subStartEl, {
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    onChange: function (selectedDates) {
        if (selectedDates.length > 0) {
            subDuePicker.set('minDate', selectedDates[0]);
        }
    }
});

const subDuePicker = flatpickr(subDueEl, {
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    onChange: function (selectedDates) {
        if (selectedDates.length > 0) {
            subStartPicker.set('maxDate', selectedDates[0]);
        }
    }
});
// --- Başlangıç min/max olarak ana task tarihleri ile sınırlı
subStartPicker.set('minDate', startTaskDatePicker.selectedDates[0] || startTaskDateEl.value);
subStartPicker.set('maxDate', dueTaskDatePicker.selectedDates[0] || dueTaskDateEl.value);
subDuePicker.set('minDate', startTaskDatePicker.selectedDates[0] || startTaskDateEl.value);
subDuePicker.set('maxDate', dueTaskDatePicker.selectedDates[0] || dueTaskDateEl.value);
let dummyData = [];
let dummyTaskData = [];
let pendingAddToMainStart = null;
let pendingAddToMainEnd = null;
// Pending seçili değerler
let pendingAttendees = [];

const checkVirtual = document.querySelector("#checkVirtualMeeting");
const txtLocation = document.querySelector("#txt-meeting-location");
const txtLink = document.querySelector("#txt-meeting-link");

const txtTaskEstimated = document.querySelector("#txt-task-estimated-hour");
const subHourInput = document.querySelector("#txt-sub-task-estimated-hour");
let subTasks = []; // Oluşturulan sub task süreleri tutulacak
let dependenciesTasks = [];
let fvDependenciesTask = null;
let checklistTasks = [];
let fvChecklistTask = null;

if (checkVirtual) {
checkVirtual.addEventListener("change", function () {
    if (this.checked) {
        // Virtual meeting seçildiyse
        txtLocation.value = "";      // Location reset
        txtLocation.disabled = true; // Opsiyonel: Location da disable edilebilir
        txtLink.disabled = false;    // Link enable
        txtLink.focus();             // Opsiyonel: focus ver
    } else {
        // Virtual meeting kaldırıldığında
        txtLocation.disabled = false;
        txtLink.value = "";          // Link sıfırlanabilir
        txtLink.disabled = true;
    }
});
}
document.addEventListener('DOMContentLoaded', function () {

    const recordType = urlParams.get('recordType') || '';
    const category = urlParams.get('category') || '';
    const workflowId = window.location.pathname.split('/')[2];


    // Sayfa başlıklarını güncelle
    updatePageTitles(recordType);

    // Input değerlerini doldur
    setInputValueIfExists('txt-record-type', recordType);
    setInputValueIfExists('txt-category', category);

    if (workflowId==='blank') {
        populateSelect('ddl-status', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });

        populateSelect('ddl-priority', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
            placeholder: 'Select priority',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });
    }
    else {


        loadWorkflow(workflowId);

    }

    loadMeetingAttendees();

    initializeFormValidation();
    initializeCharacterCounter('txt-description', 2000);
    initializeCharacterCounter('txt-name', 250);
    
    initializeCreateTaskValidation();
    bindDeleteTaskRecordEvent();
    fvSub = initializeSubTaskValidation();
    fvDependenciesTask = initializeDependenciesTaskValidation();
    fvChecklistTask = initializeChecklistTaskValidation();
});
function fillAddMeetingAttendees(users) {
    const $addSelect = $("#add-meeting-attendees");

    // Seçili değerleri sakla
    const selectedAddValues = $addSelect.val() || [];

    $addSelect.empty();
    users.forEach(user => {
        $addSelect.append(`<option value="${user.id}">${user.fullName}</option>`);
    });
    $addSelect.selectpicker("refresh");

    // Eğer pending değer varsa uygula
    if (pendingAttendees.length > 0) {
        $addSelect.selectpicker("val", pendingAttendees);
        pendingAttendees = [];
    } else if (selectedAddValues.length > 0) {
        $addSelect.selectpicker("val", selectedAddValues);
    }
}
// Main select dolduğunda
function fillMainAttendees(users) {
    const $mainSelect = $("#ddl-meeting-attendees");

    // Seçili değerleri sakla
    const selectedMainValues = $mainSelect.val() || [];

    $mainSelect.empty();
    users.forEach(user => {
        $mainSelect.append(`<option value="${user.id}">${user.fullName}</option>`);
    });
    $mainSelect.selectpicker("refresh");

    // Eğer Add Meeting selectte seçili değer varsa uygula
    const addSelectedValues = $("#add-meeting-attendees").val() || [];
    if (addSelectedValues.length > 0) {
        $mainSelect.selectpicker("val", addSelectedValues);
    } else if (pendingAttendees.length > 0) {
        $mainSelect.selectpicker("val", pendingAttendees);
        pendingAttendees = [];
    } else if (selectedMainValues.length > 0) {
        $mainSelect.selectpicker("val", selectedMainValues);
    }
}
function loadMeetingAttendees() {

    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;

    $.ajax({
        url: url,
        type: "GET",
        success: function (response) {

            if (!response || !response.data) {
                console.warn("Boş veya hatalı response:", response);
                return;
            }

            const users = response.data;
            fillAddMeetingAttendees(users);
        },
        error: function (err) {
            console.error("Attendees load error:", err);
        }
    });
}
// Add Meeting veya Main select üzerinde seçim değişirse pending sakla
$("#add-meeting-attendees").on("change", function () {
    pendingAttendees = $(this).val() || [];
});

// Main form açıldığında
function loadMainMeetingAttendees(doneCallback) {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    $.get(url, function (response) {
        if (!response || !response.data) return;
        fillMainAttendees(response.data);
        // 🔥 Callback varsa çağır
        if (typeof doneCallback === "function") {
            doneCallback();
        }
    });
}

document.querySelector('button[data-bs-target="#teamsForm"]')
    .addEventListener('shown.bs.tab', function () {

        $('.workflow-team-table').DataTable().destroy();
        const categoryId = urlParams.get('categoryId') || '';

        populateSelect('add-team-member', {
            apiUrl: `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`,
            placeholder: 'Select team member',
            valueKey: 'id',
            textKey: 'fullName',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });

        populateSelect('ddlRoles', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetTeamRolesByWorkflowCategoryId/${categoryId}`,
            placeholder: 'Select role',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });

        

        const lang = localStorage.getItem('language') || 'en';
        fetch(`/assets/lang/${lang}.json`)
            .then(response => response.json())
            .then(data => {
                const placeholderText = data["Search"] || "Search";

                // DataTable veya custom tablo init fonksiyonunu burada çağır:
                initTeamDataTable(placeholderText, data);
            })
            .catch(error => {
                console.error('Language file could not be loaded:', error);
                initTeamDataTable("Search", data); // fallback
            });

        updateTeamDashboardCards();
        initializeTeamMemberValidation();
        initializeUpdateTeamMemberValidation();
        bindDeleteRecordEvent();
    });


document.querySelector('button[data-bs-target="#tasksForm"]')
    .addEventListener('shown.bs.tab', function () {

        $('.workflow-task-list-table').DataTable().destroy();

        initializeCharacterCounter('add-task-name', 250);
        initializeCharacterCounter('add-task-description', 2000);

        populateSelect('add-task-type', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskTypes`,
            placeholder: 'Select type',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });

        populateSelect('add-task-status', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });

        populateSelect('add-task-priority', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
            placeholder: 'Select priority',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });

        populateSelect('add-task-assignee', {
            data: dummyData,
            placeholder: 'Select assignee',
            valueKey: 'userId',
            textKey: 'fullName',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });



        const selectEl = document.getElementById("add-task-type");
        // Select2 kullanılıyorsa
        if (selectEl && $(selectEl).hasClass("select2-hidden-accessible")) {
            $(selectEl).on("change", function () {
                handleTaskMeetingToggle(this.value);
            });
        } else {
            // Normal select için
            selectEl?.addEventListener("change", function () {
                handleTaskMeetingToggle(this.value);
            });
        }

        populateSelect('add-task-category', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskCategory`,
            placeholder: 'Select category',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });

        // Örnek kullanımı
        const addStart = document.querySelector("#add-meeting-start-time");
        const addEnd = document.querySelector("#add-meeting-end-time");

        // Eğer daha önce doldurulmuşsa tekrar doldurma
        if (addStart.options.length === 0) {
            generateTimeOptions().forEach(opt => {
                addStart.add(new Option(opt.text, opt.value));
                addEnd.add(new Option(opt.text, opt.value));
            });
        }


        const lang = localStorage.getItem('language') || 'en';
        fetch(`/assets/lang/${lang}.json`)
            .then(response => response.json())
            .then(data => {
                const placeholderText = data["Search"] || "Search";

                // DataTable veya custom tablo init fonksiyonunu burada çağır:
                initWorkFlowTaskDataTable(placeholderText, data);
            })
            .catch(error => {
                console.error('Language file could not be loaded:', error);
                initWorkFlowTaskDataTable("Search", data); // fallback
            });

        updateTaskDashboardCards();
        

    });

// add-meeting-start-time Select2 event
$('#add-meeting-start-time').on('change', function () {
    const value = $(this).val(); // seçilen value
    updateMainTime(value, 'start');
});

// add-meeting-end-time Select2 event
$('#add-meeting-end-time').on('change', function () {
    const value = $(this).val();
    updateMainTime(value, 'end');
});
// Toggle işlemi fonksiyonla ayrıldı
function handleTaskMeetingToggle(value) {
    const taskFields = document.querySelectorAll(".task-fields");
    const meetingFields = document.querySelectorAll(".meeting-fields");

    if (value === "2") { // string olarak kontrol et
        taskFields.forEach(el => el.style.display = "none");
        meetingFields.forEach(el => el.style.display = "block");
    } else {
        taskFields.forEach(el => el.style.display = "block");
        meetingFields.forEach(el => el.style.display = "none");
    }
}
/**
 * Sayfa başlığı ve açıklama kısmını günceller.
 */
function updatePageTitles(recordType) {
    const h4 = document.querySelector('h4.mb-1');
    const p = document.querySelector('p.mb-0');
    const h5 = document.getElementById('titleGeneralh5');
    const s = document.getElementById('subTitleGeneral');
    if (h4) h4.textContent = `New ${recordType}`;
    if (p) p.textContent = `Create a new ${recordType}`;
    if (h5) h5.textContent = `${recordType} Information`;
    if (s) s.textContent = `Enter the basic information for your new ${recordType}`;
}

/**
 * Belirtilen input varsa değerini set eder.
 */
function setInputValueIfExists(elementId, value) {
    const el = document.getElementById(elementId);
    if (el) el.value = value;
}

function initializeFormValidation() {
    const generalForm = document.getElementById('generalNewRecord');
    if (!generalForm) return;

    const fv = FormValidation.formValidation(generalForm, {
        fields: {
            txtName: {
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
            txtDescription: {
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
            ddlStatus: {
                validators: {
                    notEmpty: {
                        message: 'Status is required'
                    }
                }
            },
            ddlPriority: {
                validators: {
                    notEmpty: {
                        message: 'Priority is required'
                    }
                }
            }
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: '',
                rowSelector: function (field, ele) {
                    return '.form-control-validation';
                }
            }),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });

    const btnCreate = document.getElementById('btnCreate');
    if (btnCreate) {
        btnCreate.addEventListener('click', function () {
            fv.validate().then(function (status) {
                if (status === 'Valid') {
                    const workflowId = window.location.pathname.split('/')[2];
                    const WorkflowName = document.getElementById('txt-name').value;
                    const WorkflowCode = document.getElementById('txt-code').value;
                    const WorkflowDescription = document.getElementById('txt-description').value;
                    const recordTypeId = urlParams.get('recordTypeId') || '';
                    const categoryId = urlParams.get('categoryId') || '';
                    const WorkflowStatusId = document.getElementById('ddl-status').value;
                    const WorkflowPriorityId = document.getElementById('ddl-priority').value;
                    const userName = window.getUserName();
                    const ownerId = window.getUserId();
                    const startDateValue = document.getElementById("dtStartDate")._flatpickr.selectedDates[0];
                    const endDateValue = document.getElementById("dtEndDate")._flatpickr.selectedDates[0];
                    const startISO = startDateValue ? startDateValue.toISOString() : null;
                    const endISO = endDateValue ? endDateValue.toISOString() : null;
                    if (workflowId === 'blank') {
                        const formData = {
                            name: WorkflowName,
                            description: WorkflowDescription,
                            idCode: WorkflowCode,
                            recordTypeId: recordTypeId,
                            workflowCategoryId: categoryId,
                            startDate: startISO,
                            endDate: endISO,
                            workflowStatusId: WorkflowStatusId,
                            priorityId: WorkflowPriorityId,
                            workFlowTeams: dummyData,
                            createdBy: userName,
                            workFlowTasks: dummyTaskData,
                            ownerId: ownerId

                        };
                        fetch(`${window.ApiBaseUrl}/services/DitenPPM/Workflow/CreateWorkflow`, {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json'
                            },
                            body: JSON.stringify(formData)
                        })
                            .then(response => response.json())
                            .then(data => {

                                window.location.href = `/ppm/workflow-overview`;


                            })
                            .catch(error => {
                                console.error(error);
                                showToast('Kayıt sırasında bir hata oluştu.', "error");

                            });

                    }
                    else {
                        const formData = {
                            id: workflowId,
                            name: WorkflowName,
                            description: WorkflowDescription,
                            idCode: WorkflowCode,
                            recordTypeId: recordTypeId,
                            workflowCategoryId: categoryId,
                            startDate: startISO,
                            endDate: endISO,
                            workflowStatusId: WorkflowStatusId,
                            priorityId: WorkflowPriorityId,
                            workFlowTeams: dummyData,
                            createdBy: userName,
                            workFlowTasks: dummyTaskData

                        };
                        fetch(`${window.ApiBaseUrl}/services/DitenPPM/Workflow/UpdateWorkflow`, {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json'
                            },
                            body: JSON.stringify(formData)
                        })
                            .then(response => response.json())
                            .then(data => {

                                window.location.href = `/ppm/workflow-overview`;


                            })
                            .catch(error => {
                                console.error(error);
                                showToast('Kayıt sırasında bir hata oluştu.', "error");

                            });

                    }


                    





                    console.log('Form valid! Gönderim yapılabilir.');
                    // Burada AJAX ile backend'e gönderebilirsin
                } else {
                    console.log('Form invalid! Lütfen hataları düzeltin.');
                }
            });
        });
    }
}

/**
 * Team Datatableını initialize eder,
 */
function initTeamDataTable(placeholderText, lanData) {

    dt_workflow_team_table = document.querySelector('.workflow-team-table');
    if (!dt_workflow_team_table) return;

    if (dt_workflow_team_table) {

        

        dt_workflow_team = new DataTable(dt_workflow_team_table, {
            data: dummyData,
            //ajax: {
            //    url: `${window.ApiBaseUrl}/services/PvSurvey/Survey/GetSurveys`,
            //    method: 'GET',
            //    dataSrc: 'data',
            //    error: function (jqxhr, textStatus, errorThrown) {
            //        // Bu callback sadece DataTable özelinde hata olursa çalışır
            //        console.error("DataTable Error:", jqxhr.status, errorThrown);
            //        if (jqxhr.status !== 200) {
            //            window.location.href = '/pages-misc-error.html?code=' + jqxhr.status; // Hata kodu ile yönlendirme
            //        }
            //    }
            //},
            columns: [
                { data: 'id', visible: false },                          // hidden
                { data: 'fullName' },                    // Name
                { data: 'roles' },                       // Roles
                { data: null },                   // Total Hour
                { data: 'skills' },                      // Skills
                { data: null },                          // Tasks
                { data: 'teamStatusName' },              // Status
                { data: null }                           // Actions
            ],
            columnDefs: [
                {
                    responsivePriority: 2,
                    targets: 0,
                    visible: false

                },
                {
                    targets: 1,
                    render: (data) => `
                    <span class="fw-medium">${data}</span>
                `
                },
                {
                    targets: 2, // Roles → Badge list
                    render: (data) => {
                        if (!data || data.length === 0) return "-";
                        return data
                            .map(r => `<span class="badge bg-label-primary me-1">${r.name}</span>`)
                            .join('');
                    }
                },
                {
                    targets: 3,
                    render: (data, type, row) => {
                        const original = dummyData.find(x => x.id === row.id);
                        const userId = original?.userId;

                        const hours = getUserTotalHours(userId);

                        return `${hours} h`;
                    }
                },
                {
                    targets: 4, // Skills → Badge list
                    render: (data) => {
                        if (!data || data.length === 0) return "-";
                        return data
                            .map(s => `<span class="badge bg-label-secondary me-1">${s}</span>`)
                            .join('');
                    }
                },
                {
                    targets: 5,
                    render: (data, type, full) => {
                        const stats = getUserTaskStats(full.userId);
                        return `<span class="fw-semibold">${stats.completed} / ${stats.total}</span>`;
                    }
                },
                {
                    targets: 6, // Status
                    render: (data, type, row) => {
                        const color = row.teamStatus ? "primary" : "secondary";
                        return `<span class="badge bg-label-${color}">${data}</span>`;
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    orderable: false,
                    render: (data, type, full) => {
                        return `
                            <div class="d-flex align-items-center">
                                <a href="javascript:;" class="btn btn-icon edit-team-record" data-id="${full.id}" data-name="${full.fullName}">
                                    <i class="icon-base bx bx-edit-alt icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon delete-record" data-id="${full.id}">
                                    <i class="icon-base bx bx-trash icon-md"></i>
                                </a>
                            </div>
                        `;
                    }
                }
            ],
            select: {
                style: 'multi',
                selector: 'td:nth-child(2)'
            },
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
                                placeholder: placeholderText,
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
                                    text: '<i class="icon-base bx bx-plus"></i><span class="d-none d-sm-inline-block">Add Team Member</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#offcanvasCreateTeamMember'
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
                paginate: {
                    next: '<i class="icon-base bx bx-chevron-right scaleX-n1-rtl icon-18px"></i>',
                    previous: '<i class="icon-base bx bx-chevron-left scaleX-n1-rtl icon-18px"></i>'
                },
                sInfo: lanData?.DataTable?.sInfo || "Showing _START_ to _END_ of _TOTAL_ entries",
                sInfoEmpty: lanData?.DataTable?.sInfoEmpty || "Showing 0 to 0 of 0 entries",
                sInfoFiltered: lanData?.DataTable?.sInfoFiltered || "(filtered from _MAX_ total entries)",
                sLengthMenu: lanData?.DataTable?.sLengthMenu || "Show _MENU_ entries"
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
            initComplete: function () {
                modifyDataTableLayout();

                //const params = new URLSearchParams(window.location.search);
                //const filterSurveyId = params.get('filterSurveyId');

                //const data = dt_survey_list.rows().data().toArray();

                //// ID'si eşleşen satırı bul
                //const matched = data.find(item => item.id === filterSurveyId);

                //if (matched) {
                //    const surveyName = matched.name;

                //    // Arama kutusuna survey adını yaz
                //    $('.dt-search input').val(surveyName);

                //    // DataTable'da filtre uygula
                //    dt_survey_list.search(surveyName).draw();
                //}


            },
            drawCallback: function () {
                modifyDataTableLayout(); // her yeniden çizimde stil uygula (sayfalama, filtre vs.)
            }
        });

    }

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
/**
 * Task Datatableını initialize eder,
 */
window.taskViewFilter = "all";
function initWorkFlowTaskDataTable(placeholderText, lanData) {

    dt_workflow_task_table = document.querySelector('.workflow-task-list-table');
    if (!dt_workflow_task_table) return;

    if (window.dt_workflow_task && $.fn.DataTable.isDataTable(dt_workflow_task_table)) {
        dt_workflow_task.destroy();
    }

    if (dt_workflow_task_table) {
        dt_workflow_task = new DataTable(dt_workflow_task_table, {
            data: dummyTaskData,
            columns: [
                { data: 'id' },                          // hidden
                { data: 'name' },                          // hidden
                { data: 'typeName' },                    // Name
                { data: 'categoryName' },                       // Roles
                { data: 'ownerName' },
                { data: null },// Total Hour
                { data: 'priorityName' },                      // Skills
                { data: 'progress' },                          // Tasks
                {
                    data: null,
                    render: (data, type, row) => {
                        // Date objesine çevir
                        const start = new Date(row.startDateTime);
                        const end = new Date(row.endDateTime);

                        // Tarih + Saat formatları
                        const format = (date) => {
                            const day = String(date.getDate()).padStart(2, '0');
                            const month = String(date.getMonth() + 1).padStart(2, '0');
                            const year = date.getFullYear();

                            const hours = String(date.getHours()).padStart(2, '0');
                            const minutes = String(date.getMinutes()).padStart(2, '0');

                            return {
                                date: `${day}.${month}.${year}`,
                                time: `${hours}:${minutes}`
                            };
                        };

                        const s = format(start);
                        const e = format(end);

                        return `
            <div class="d-flex flex-column">
                <small>S: ${s.date} ${s.time}</small>
                <small>E: ${e.date} ${e.time}</small>
            </div>
        `;
                    }
                },

            // Status
                {
                    data: null,
                    render: (data, type, row) => {
                        const formattedActual = formatDuration(row.actualHour);  // saniye → h/m/s
                        return `${formattedActual} / ${row.estimatedHour}m`;
                    }
                },              // Status
                { data: 'statusName' },              // Status
                { data: null }                           // Actions
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

                        // Max karakterler
                        const maxDescLength = 50;
                        const maxNameLength = 30;

                        // Kısaltılmış metinler
                        const shortDesc = description.length > maxDescLength
                            ? description.substring(0, maxDescLength) + '...'
                            : description;

                        const shortName = name.length > maxNameLength
                            ? name.substring(0, maxNameLength) + '...'
                            : name;

                        // Tooltip sadece description uzun ise
                        const tooltipAttr = description.length > maxDescLength
                            ? `data-bs-toggle="tooltip" title="${description.replace(/"/g, '&quot;')}"`
                            : '';

                        const rowOutput = `
<div class="d-flex justify-content-start align-items-center survey-name">
  <div class="d-flex flex-column text-wrap">
    <span class="fw-medium text-truncate" style="max-width:250px;" ${tooltipAttr}>${shortName}</span>
    <small class="text-break task-desc text-truncate" style="max-width:250px;" ${tooltipAttr}>${shortDesc}</small>
  </div>
</div>`;

                        return rowOutput;
                    }
                },
                {
                    targets: 2,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        let color = 'primary';
                        const typeId = full['typeId'];
                        if (typeId === "2") color = 'info';
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

                        if (!Array.isArray(row.assignee) || row.assignee.length === 0) {
                            return `<span class="badge bg-label-secondary rounded-circle d-inline-flex align-items-center justify-content-center"
                           style="width:32px; height:32px; font-size:12px;">-</span>`;
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
                    render: (data, type, full) => `
                    <div class="d-flex align-items-center">
                        <div class="progress w-100 me-2" style="height: 6px;">
                            <div class="progress-bar bg-primary" role="progressbar"
                                style="width: ${parseInt(full.progress)}%;"
                                aria-valuenow="${parseInt(full.progress)}"
                                aria-valuemin="0" aria-valuemax="100">
                            </div>
                        </div>
                        <small>${full.progress}</small>
                    </div>
                `
                },
                {
                    targets: 10,
                    responsivePriority: 1,
                    render: (data, type, full) => {
                        let color = 'secondary';
                        const taskStatusId =Number(full['statusId']);

                        if (taskStatusId === 3) color = 'success';
                        else if (taskStatusId === 2) color = 'info';
                        else if (taskStatusId === 1) color = 'warning';
                        else if (taskStatusId === 4) color = 'danger';
                        return `<span class="badge bg-label-${color}">${data}</span>`;
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    responsivePriority: 1,
                    orderable: false,
                    render: (data, type, full) => {

                        const taskStatusId = Number(full['statusId']);

                        // ✔ Eğer completed ise hiçbir action butonu gösterme
                        if (taskStatusId === 3) {
                            return `<div class="text-muted small">—</div>`;
                        }

                        // ✔ Normal durumda edit + delete
                        return `
        <div class="d-flex align-items-center">
            <a href="javascript:;" class="btn btn-icon edit-task-record" data-id="${full.id}" data-name="${full.fullName}">
                <i class="icon-base bx bx-edit-alt icon-md"></i>
            </a>
            <a href="javascript:;" class="btn btn-icon delete-task-record" data-id="${full.id}">
                <i class="icon-base bx bx-trash icon-md"></i>
            </a>
        </div>
    `;
                    }
                }
            ],
            select: {
                style: 'multi',
                selector: 'td:nth-child(2)'
            },
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
                                placeholder: placeholderText,
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
                                    extend: 'collection',
                                    className: 'btn btn-label-secondary dropdown-toggle',
                                    text: `
        <span class="d-flex align-items-center gap-2">
            <i class="bx bx-filter-alt"></i>
            <span class="d-none d-sm-inline-block">Task Filter</span>
        </span>
    `,
                                    buttons: [
                                        {
                                            text: '<i class="icon-base bx bx-show me-1"></i> Show All',
                                            className: 'dropdown-item',
                                            action: () => {
                                                window.taskViewFilter = "all";
                                                dt_workflow_task.draw();
                                            }
                                        },
                                        {
                                            text: '<i class="icon-base bx bx-list-ul me-1"></i> Only Tasks',
                                            className: 'dropdown-item',
                                            action: () => {
                                                window.taskViewFilter = "tasks";
                                                dt_workflow_task.draw();
                                            }
                                        },
                                        {
                                            text: '<i class="icon-base bx bx-git-branch me-1"></i> Only Subtasks',
                                            className: 'dropdown-item',
                                            action: () => {
                                                window.taskViewFilter = "subtasks";
                                                dt_workflow_task.draw();
                                            }
                                        }
                                    ]
                                },
                                {
                                    text: '<i class="icon-base bx bx-plus"></i><span class="d-none d-sm-inline-block">Create Task</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#offcanvasCreateTask'
                                    },
                                    action: function (e, dt, node, config) {

                                        $('#add-task-type').val(null).trigger('change');
                                        $('#add-task-category').val(null).trigger('change');
                                        $('#add-task-assignee').val(null).trigger('change');
                                        $('#add-task-status').val(null).trigger('change');
                                        $('#add-task-priority').val(null).trigger('change');
                                        $('#add-meeting-attendees').val(null).trigger('change');
                                        $('#add-meeting-start-time').val(null).trigger('change');
                                        $('#add-meeting-end-time').val(null).trigger('change');

                                        const taskFields = document.querySelectorAll(".task-fields");
                                        const meetingFields = document.querySelectorAll(".meeting-fields");
                                        taskFields.forEach(el => el.style.display = "none");
                                        meetingFields.forEach(el => el.style.display = "none");
                                       



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
                paginate: {
                    next: '<i class="icon-base bx bx-chevron-right scaleX-n1-rtl icon-18px"></i>',
                    previous: '<i class="icon-base bx bx-chevron-left scaleX-n1-rtl icon-18px"></i>'
                },
                sInfo: lanData?.DataTable?.sInfo || "Showing _START_ to _END_ of _TOTAL_ entries",
                sInfoEmpty: lanData?.DataTable?.sInfoEmpty || "Showing 0 to 0 of 0 entries",
                sInfoFiltered: lanData?.DataTable?.sInfoFiltered || "(filtered from _MAX_ total entries)",
                sLengthMenu: lanData?.DataTable?.sLengthMenu || "Show _MENU_ entries"
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
            initComplete: function () {
                //modifyDataTableLayout();

                //const params = new URLSearchParams(window.location.search);
                //const filterSurveyId = params.get('filterSurveyId');

                //const data = dt_survey_list.rows().data().toArray();

                //// ID'si eşleşen satırı bul
                //const matched = data.find(item => item.id === filterSurveyId);

                //if (matched) {
                //    const surveyName = matched.name;

                //    // Arama kutusuna survey adını yaz
                //    $('.dt-search input').val(surveyName);

                //    // DataTable'da filtre uygula
                //    dt_survey_list.search(surveyName).draw();
                //}


            },
            drawCallback: function () {
                modifyDataTableLayout(); // her yeniden çizimde stil uygula (sayfalama, filtre vs.)
            }
        });

    }
    $.fn.dataTable.ext.search.push(function (settings, data, dataIndex) {
        if (settings.nTable !== dt_workflow_task_table) return true; // sadece ilgili tabloyu filtrele

        const row = dt_workflow_task.row(dataIndex).data();
        if (!row) return true;

        const isSubtask = !!row.parentTaskId; // parentTaskId varsa subtask’tır

        switch (window.taskViewFilter) {
            case "tasks":
                return !isSubtask;
            case "subtasks":
                return isSubtask;
            default:
                return true;
        }
    });

}
// 🌟 Custom DataTable filter
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


document.getElementById("offcanvasCreateTask")
    .addEventListener("shown.bs.offcanvas", function () {

        // Formu resetle
        const form = document.getElementById("createTask");
        if (form) form.reset();

        // FormValidation temizle
        if (typeof initializeCreateTaskValidation === "function") {
            const fv = FormValidation.getInstance(form);
            if (fv) fv.resetForm(true);
        }
        $('#add-task-name').val('');
        $('#add-task-description').val('');
        $('#add-task-estimated-hour').val('');
        // Select2 temizle
        $('#add-task-type').val(null).trigger('change');
        $('#add-task-category').val(null).trigger('change');
        $('#add-task-assignee').val(null).trigger('change');
        $('#add-task-status').val(null).trigger('change');
        $('#add-task-priority').val(null).trigger('change');

        // Meeting fields
        $('#add-meeting-attendees').val(null).trigger('change');
        $('#add-meeting-start-time').val(null).trigger('change');
        $('#add-meeting-end-time').val(null).trigger('change');
        $('#add-meeting-start-date').val('');
        $('#add-meeting-end-date').val('');

        // Flatpickr temizle
        if (document.getElementById("add-task-due-date")._flatpickr)
            document.getElementById("add-task-due-date")._flatpickr.clear();

        if (document.getElementById("add-meeting-start-date")._flatpickr)
            document.getElementById("add-meeting-start-date")._flatpickr.clear();

        if (document.getElementById("add-meeting-end-date")._flatpickr)
            document.getElementById("add-meeting-end-date")._flatpickr.clear();

        // Hidden/Shown logic reset
        document.querySelectorAll(".task-fields").forEach(el => el.style.display = "none");
        document.querySelectorAll(".meeting-fields").forEach(el => el.style.display = "none");


    });

function initializeTeamMemberValidation() {
    const form = document.getElementById('createTeamMember');
    if (!form) return;

    const fv = FormValidation.formValidation(form, {
        fields: {
            teamMember: {
                validators: {
                    notEmpty: {
                        message: 'Team member is required'
                    }
                }
            },
            ddlRoles: {
                validators: {
                    notEmpty: {
                        message: 'At least one role must be selected'
                    }
                }
            },
            skills: {
                validators: {
                    callback: {
                        message: 'Skills must be comma-separated words (e.g., "Project Manager, BA")',
                        callback: function (input) {
                            const value = input.value.trim();

                            // Boşsa OK (opsiyonel ise)
                            if (value === "") return true;

                            // Regex doğrulaması
                            const regex = /^([A-Za-zğüşöçıİĞÜŞÖÇ]+(?:\s[A-Za-zğüşöçıİĞÜŞÖÇ]+){0,2})(\s*,\s*[A-Za-zğüşöçıİĞÜŞÖÇ]+(?:\s[A-Za-zğüşöçıİĞÜŞÖÇ]+){0,2})*$/;
                            return regex.test(value);
                        }
                    }
                }
            }
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: '',
                rowSelector: function (field, ele) {
                    return '.form-control-validation';
                }
            }),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });

    // Submit
    const btn = form.querySelector('.data-submit-add');
    btn.addEventListener('click', function () {
        fv.validate().then(function (status) {


            if (status === 'Valid') {

                const newId = generateObjectId(dummyData);

                const userId = $('#add-team-member').val();
                const fullName = $('#add-team-member').find('option:selected').text();
                const selectedRoles = $('#ddlRoles').val() || [];
                const selectedRolesFormatted = selectedRoles.map(id => {
                    return {
                        id: id,
                        name: $('#ddlRoles option[value="' + id + '"]').text()
                    };
                });

                const skillsText = $('#add-skills').val().trim();
                const skillsArray = skillsText ? skillsText.split(',').map(s => s.trim()) : [];

                // ❌ Check for duplicate userId
                const exists = dummyData.some(member => member.userId === userId);
                if (exists) {
                    showToast(`${fullName} has already been added!`, "error");
                    return; // ekleme durdur
                }



                const newMember = {
                    id: newId,
                    userId: userId,
                    fullName: fullName,
                    roles: selectedRolesFormatted,
                    totalHour: 0,
                    skills: skillsArray,
                    completedTask: 0,
                    totalTask: 0,
                    teamStatus: true,
                    teamStatusName: "active"
                };
                dummyData.push(newMember);

                dt_workflow_team.row.add(newMember).draw(false);
                $('#offcanvasCreateTeamMember').offcanvas('hide');
                form.reset();
                // Select2 reset
                $('#add-team-member').val(null).trigger('change');

                // Bootstrap-select reset
                $('#ddlRoles').selectpicker('deselectAll');
                $('#ddlRoles').selectpicker('refresh');
                updateTeamDashboardCards();
                console.log("Team member valid!");
                // burada ekleme işlemi
            } else {
                console.log("Team member invalid");
            }
        });
    });
}

// 1️⃣ Edit butonuna tıklama
$(document).on('click', '.edit-team-record', function () {
    const id = $(this).data('id'); // satır ID'si
    
    const member = dummyData.find(m => m.id === id);
    const categoryId = urlParams.get('categoryId') || '';

    if (!member) return;

  
    populateSelect('update-team-member', {
        apiUrl: `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`,
        placeholder: 'Select team member',
        valueKey: 'id',
        textKey: 'fullName',
        selectedValue: member.userId || 0  // Tek kayıt varsa otomatik seçer

    });

    const ddlRoles = $('#updateddlRoles');
    ddlRoles.selectpicker('destroy');
    ddlRoles.empty(); // Önce mevcut optionları temizle
    // API'den role listesini al
    $.ajax({
        url: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetTeamRolesByWorkflowCategoryId/${categoryId}`,
        type: 'GET',
        success: function (response) {

            const roles = response.data || [];
            // roles array: [{id: 1, name: 'Manager'}, ...]
            roles.forEach(role => {
                const isSelected = member.roles.some(r => r.id == role.id); // seçili mi
                const option = $('<option>')
                    .val(role.id)
                    .text(role.name)
                    .prop('selected', isSelected);
                ddlRoles.append(option);
            });

            // Eğer bootstrap-select kullanıyorsan refresh et
            ddlRoles.selectpicker('refresh');
        },
        error: function (err) {
            console.error('Roles load error:', err);
        }
    });

    $('#update-skills').val(member.skills.join(', ')); // Skills textarea
    // Güncellenecek rowId'yi sakla
    $('#updateTeamMember').attr('data-id', id);

    const offcanvasEl = document.getElementById('offcanvasUpdateTeamMember');
    const bsOffcanvas = new bootstrap.Offcanvas(offcanvasEl);
    bsOffcanvas.show();
});




$(document).on('click', '.edit-task-record', function () {
    const id = $(this).data('id'); // satır ID'si

    const task = dummyTaskData.find(m => m.id === id);
    const recordType = urlParams.get('recordType') || '';

    const btn = document.getElementById("btnCreateTask");
    btn.textContent = "Update Task";
    btn.setAttribute("data-mode", "edit");
    btn.setAttribute("data-edit-id", task.id);

    document.getElementById("btnCreateTask").textContent = "Update Task";
    const taskTypeText = task?.typeName || 'Task';
    document.getElementById('hdrTask').textContent = `Edit ${taskTypeText} / ${task.name}`;
    document.getElementById('pTask').textContent =
        `Edit a  ${taskTypeText} to your workspace and assign it to a ${recordType} and group`;

    handleTaskTabs(String(task.typeId));
    if (String(task.typeId) === '1') toggleForms('taskFormContainer', 'meetingFormContainer');
    if (String(task.typeId) === '2') toggleForms('meetingFormContainer', 'taskFormContainer');
    updateEditFormFields(String(task.typeId),task);

    document.getElementById('normalFormContainer').classList.add('d-none');
    document.getElementById('fullFormContainer').classList.remove('d-none');
});


function updateEditFormFields(taskTypeVal,task) {

    const taskName = task?.name ?? '';

    const IntiativeEl = document.getElementById('txt-name');
    const intivativeName = IntiativeEl ? IntiativeEl.value : '';

    const taskDescription = task?.description ?? '';

    const taskCategory = task?.categoryId ?? '';
    ResetSubTaskFormFields();
    ResetDependenciesTaskFormFields();
    ResetChecklistTaskFormFields();
    if (taskTypeVal === '1') {
        // TASK FORM ALANLARI AYARLARI

        $('#txt-task-name').val(taskName);
        $('#txt-task-description').val(taskDescription);
        $('#txt-intiatives').val(intivativeName);

        const selectedAssigneeId =
            task?.assignee && task.assignee.length > 0
                ? task.assignee[0].id
                : null;

        populateSelect('ddl-task-assignee', {
            data: dummyData,
            placeholder: 'Select assignee',
            valueKey: 'userId',
            textKey: 'fullName',
            selectedValue: selectedAssigneeId

        });


        populateSelect('ddl-task-category', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskCategory`,
            placeholder: 'Select category',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: taskCategory

        });

        const selectedStatusId = task?.statusId ?? 0;
        populateSelect('ddl-task-status', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: selectedStatusId

        });

        const selectedPriorityId = task?.priorityId ?? 0;


        populateSelect('ddl-task-priority', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
            placeholder: 'Select priority',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: selectedPriorityId

        });

        const estimatedHour = task?.estimatedHour ?? 0;
        $('#txt-task-estimated-hour').val(estimatedHour);

        const { date: startDate, time: startTime } = splitDateTime(task.startDateTime);
        const { date: endDate, time: endTime } = splitDateTime(task.endDateTime);

        document.getElementById("dtTaskStartDate")._flatpickr.setDate(startDate, true);
        document.getElementById("dtTaskDueDate")._flatpickr.setDate(endDate, true);


        // -------------------------------
        // MEETING FORM ALANLARINI RESETLE
        // -------------------------------
        $('#txt-meeting-name').val('');
        $('#txt-workflow').val('');
        $('#txt-meeting-description').val('');
        $('#checkVirtualMeeting').prop('checked', false);
        $('#txt-meeting-location').val('');
        $('#txt-meeting-link').val('');
        $('#ddl-meeting-attendees').val([]).trigger('change');
        $('#ddl-meeting-category').val(null).trigger('change');
        $('#txt-meeting-start-date').val('');
        $('#ddl-meeting-start-time').val(null).trigger('change');
        $('#txt-meeting-end-date').val('');
        $('#ddl-meeting-end-time').val(null).trigger('change');


        //-------------------SUB TASKS-------------------
        populateSelect('ddl-sub-task-assignee', {
            data: dummyData,
            placeholder: 'Select assignee',
            valueKey: 'userId',
            textKey: 'fullName',
            autoSelectIfSingle: true

        });
        populateSelect('ddl-sub-task-category', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskCategory`,
            placeholder: 'Select category',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true

        });
        populateSelect('ddl-sub-task-status', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true

        });
        populateSelect('ddl-sub-task-priority', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
            placeholder: 'Select priority',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true

        });
        updateSubTaskMax();
        subTasks = task?.subTasks || [];
        renderAllSubTasks();

        //-------------------DEPENDENCIES TASKS-------------------
        debugger;
        const filteredTasks = filterDependenciesForTask(task.id, dummyTaskData);
        populateSelect('ddl-dependencies-task', {
            data: filteredTasks,
            placeholder: 'Select task',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true

        });
        populateSelect('ddl-dependencies-type', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetDependenciesType`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            autoSelectIfSingle: true

        });
        dependenciesTasks = task?.dependenciesTasks || [];
        renderAllDependenciesTasks();
        //-------------------CHECKLIST TASKS-------------------
        checklistTasks = task?.checklistTasks || [];
        renderAllChecklistTasks();



    }
    else if (taskTypeVal === '2') {

        // MEETING FORM ALANLARI AYARLARI
        $('#checkVirtualMeeting').prop('checked', task.isVirtual === true);
        $('#txt-meeting-location').val('');
        $('#txt-meeting-link').val('');



        // Checkbox duruma göre enable/disable ayarı
        // toplantı virtual ise location disable, link enable
        if (task.isVirtual === true) {
            $('#txt-meeting-location').prop('disabled', true);
            $('#txt-meeting-link').prop('disabled', false);
        } else {
            $('#txt-meeting-location').prop('disabled', false);
            $('#txt-meeting-link').prop('disabled', true);
        }



        $('#txt-meeting-location').val(task.location);
        $('#txt-meeting-link').val(task.meetingLink);
        $('#txt-meeting-name').val(taskName);
        $('#txt-workflow').val(intivativeName);
        $('#txt-meeting-description').val(taskDescription);
        populateSelect('ddl-meeting-category', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskCategory`,
            placeholder: 'Select category',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: taskCategory

        });
        //add-meeting-start-time
        //add-meeting-end-time
        // Örnek kullanımı
        const mainStart = document.querySelector("#ddl-meeting-start-time");
        const mainEnd = document.querySelector("#ddl-meeting-end-time");

        const { date: startDate, time: startTime } = splitDateTime(task.startDateTime);
        const { date: endDate, time: endTime } = splitDateTime(task.endDateTime);

        // 🟢 Tarihleri set et
        document.getElementById("txt-meeting-start-date")._flatpickr.setDate(startDate, true);
        document.getElementById("txt-meeting-end-date")._flatpickr.setDate(endDate, true);

        // 🟢 Saat select’lerini doldur
        if (mainStart.options.length === 0) {
            generateTimeOptions().forEach(opt => {
                mainStart.add(new Option(opt.text, opt.value));
                mainEnd.add(new Option(opt.text, opt.value));
            });
        }

        // 🟢 Gelen saat değerleri optionlarda var mı kontrol et
        const fixedStartTime = hasOption(mainStart, startTime)
            ? startTime
            : roundTimeToOptions(startTime);

        const fixedEndTime = hasOption(mainEnd, endTime)
            ? endTime
            : roundTimeToOptions(endTime);

        // 🟢 Saatleri select'e set et
        mainStart.value = fixedStartTime;
        mainEnd.value = fixedEndTime;



        // 1️⃣ Attendees id listesi
        const attendeeIds = (task?.assignee || []).map(a => a.id);

        // 2️⃣ Eğer attendees listesi boşsa hiçbir şey seçme
        if (!attendeeIds.length) {
            $('#ddl-meeting-attendees').selectpicker('val', []);
            return;
        }

        // 3️⃣ Select'i doldurup sonradan değerleri set etmek için
        loadMainMeetingAttendees(() => {
            $('#ddl-meeting-attendees').selectpicker('val', attendeeIds);
        });

        // -----------------------------------------
        // TASK FORM ALANLARINI RESETLE
        // -----------------------------------------
        $('#txt-task-name').val('');
        $('#txt-intiatives').val('');
        $('#txt-task-description').val('');
        $('#ddl-task-assignee').val([]).trigger('change');
        $('#ddl-task-category').val(null).trigger('change');
        $('#ddl-task-status').val(null).trigger('change');
        $('#ddl-task-priority').val(null).trigger('change');
        $('#txt-task-estimated-hour').val('');
        $('#dtTaskStartDate').val('');
        $('#dtTaskDueDate').val('');

        loadAgendaItemsForEdit(task);
    }
}

function loadAgendaItemsForEdit(task) {

    const container = document.getElementById("agendaDetailView");
    container.innerHTML = ""; // önce temizle
    agendaCounter = 0;        // yeniden say

    (task.agendaItems || []).forEach(item => {

        // Yeni boş item oluştur
        addAgendaItem();  // bu fonksiyon HTML elementini append ediyor

        // En son eklenen item
        const lastItem = container.querySelectorAll(".agenda-item");
        const newItem = lastItem[lastItem.length - 1];

        // HTML elementler
        const startDateInput = newItem.querySelector(".agenda-start-date");
        const endDateInput = newItem.querySelector(".agenda-end-date");
        const startTimeSelect = newItem.querySelector(".agenda-start-time");
        const endTimeSelect = newItem.querySelector(".agenda-end-time");
        const attendeesSelect = newItem.querySelector(".agenda-attendees");
        const titleInput = newItem.querySelector(".agenda-title");

        // Tarih ve saat ayrıştır
        const { date: startDate, time: startTime } = splitDateTimeLocal(item.startDateTime);
        const { date: endDate, time: endTime } = splitDateTimeLocal(item.endDateTime);
        

        // Tarihleri flatpickr ile set
        startDateInput._flatpickr.setDate(startDate, true);
        endDateInput._flatpickr.setDate(endDate, true);

        // Saatler (async)
        setSelectValueWhenReady(startTimeSelect, startTime);
        setSelectValueWhenReady(endTimeSelect, endTime);

        // Title set
        titleInput.value = item.title || "";
        // Select2 attendees set
        const attendeeIds = item.assignee.map(a => a.id);

        loadAgendaAttendees(attendeesSelect, () => {
            $(attendeesSelect).val(attendeeIds).trigger("change");
        });
    });

    reorderAgendaNumbers();
}

function setSelectValueWhenReady(selectElement, value) {
    const trySet = () => {
        const hasOption = [...selectElement.options].some(o => o.value == value);

        if (hasOption) {
            selectElement.value = value;
            $(selectElement).trigger("change");
        } else {
            // Eğer henüz options yüklenmediyse 50ms bekle ve tekrar dene
            setTimeout(trySet, 50);
        }
    };

    trySet();
}

function splitDateTimeLocal(dateTime) {
    const d = new Date(dateTime);

    const pad = (n) => String(n).padStart(2, "0");

    const date = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

    const time = `${pad(d.getHours())}:${pad(d.getMinutes())}`;

    return { date, time };
}
function splitDateTime(isoString) {
    const d = new Date(isoString);

    // Tarih → Y-M-D formatı (flatpickr bunu ister)
    const date = d.toISOString().split("T")[0];

    // Saat → HH:mm formatı (senin select option’ların bunu ister)
    const hours = String(d.getHours()).padStart(2, "0");
    const minutes = String(d.getMinutes()).padStart(2, "0");

    return {
        date: date,        // 2025-11-18
        time: `${hours}:${minutes}` // 09:37 → senin optionlarında 09:30 yoksa 09:30'a yuvarlayabilirsin
    };
}

function initializeUpdateTeamMemberValidation() {
    const form = document.getElementById('updateTeamMember');
    if (!form) return;

    const fv = FormValidation.formValidation(form, {
        fields: {
            teamMember: {
                selector: '#update-team-member',
                validators: {
                    notEmpty: {
                        message: 'Team member is required'
                    }
                }
            },
            ddlRoles: {
                selector: '#updateddlRoles',
                validators: {
                    notEmpty: {
                        message: 'At least one role must be selected'
                    }
                }
            },
            skills: {
                selector: '#update-skills',
                validators: {
                    callback: {
                        message: 'Skills must be comma-separated words (e.g., "Project Manager, BA")',
                        callback: function (input) {
                            const value = input.value.trim();

                            // Boşsa OK (opsiyonel ise)
                            if (value === "") return true;

                            // Regex doğrulaması
                            const regex = /^([A-Za-zğüşöçıİĞÜŞÖÇ]+(?:\s[A-Za-zğüşöçıİĞÜŞÖÇ]+){0,2})(\s*,\s*[A-Za-zğüşöçıİĞÜŞÖÇ]+(?:\s[A-Za-zğüşöçıİĞÜŞÖÇ]+){0,2})*$/;
                            return regex.test(value);
                        }
                    }
                }
            }
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: '',
                rowSelector: function (field, ele) {
                    return '.form-control-validation';
                }
            }),
            submitButton: new FormValidation.plugins.SubmitButton(),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });

    handleUpdateFormSubmit(fv);
}


function handleUpdateFormSubmit(fv) {
    fv.on('core.form.valid', function () {
        const form = document.getElementById('updateTeamMember');
        const rowId = $('#updateTeamMember').attr('data-id');
        const member = dummyData.find(m => m.id === rowId);
        if (!member) {
            return;
        }
        const fullName = $('#update-team-member').find('option:selected').text();
        const userId = $('#update-team-member').val();
        const selectedRoles = $('#updateddlRoles').val() || [];
        const selectedRolesFormatted = selectedRoles.map(id => {
            return {
                id: id,
                name: $('#updateddlRoles option[value="' + id + '"]').text()
            };
        });
        const skillsText = $('#update-skills').val().trim();

        const skillsArray = skillsText ? skillsText.split(',').map(s => s.trim()) : [];

        // ❌ Check for duplicate userId
        const exists = dummyData.some(member => member.userId === userId && member.id !== rowId);
        if (exists) {
            showToast(`${fullName} has already been added!`, "error");
            return; // ekleme durdur
        }
        // Update dummyData
        member.userId = userId;
        member.fullName = fullName;
        member.roles = selectedRolesFormatted;
        member.skills = skillsArray;

        // DataTable güncelle
        const rowNode = dt_workflow_team.row(idx => dt_workflow_team.row(idx).data().id === member.id);
        if (rowNode.node()) {
            rowNode.data(member).invalidate().draw(false);
        }
        updateTeamDashboardCards();
        $('#offcanvasUpdateTeamMember').offcanvas('hide');
        form.reset();
        // Select2 reset
        $('#update-team-member').val(null).trigger('change');

        // Bootstrap-select reset
        $('#updateddlRoles').selectpicker('deselectAll');
        $('#updateddlRoles').selectpicker('refresh');


    });
}


function bindDeleteRecordEvent() {
    let recordIdToDelete = null;
    let rowToDelete = null;

    document.addEventListener('click', function (e) {
        if (e.target.closest('.delete-record')) {
            const button = e.target.closest('.delete-record');
            recordIdToDelete = button.getAttribute('data-id');
            rowToDelete = button.closest('tr');

            const deleteModal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
            deleteModal.show();
        }
    });

    document.getElementById('confirmDeleteBtn').addEventListener('click', async function () {
        if (!recordIdToDelete) return;

        // Silinecek kaydın index’i
        const index = dummyData.findIndex(item => item.id == recordIdToDelete);

        if (index !== -1) {
            dummyData.splice(index, 1); // listeden kaldır

            // DataTable varsa yenile
            const tableElement = $('.workflow-team-table');
            if ($.fn.DataTable.isDataTable(tableElement)) {
                tableElement.DataTable().clear().rows.add(dummyData).draw();
            }
            updateTeamDashboardCards();
        }

        bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();

        // Değişkenleri sıfırla
        recordIdToDelete = null;
        rowToDelete = null;
    });
}


function bindDeleteTaskRecordEvent() {
    let recordIdToDelete = null;
    let rowToDelete = null;

    document.addEventListener('click', function (e) {
        if (e.target.closest('.delete-task-record')) {
            const button = e.target.closest('.delete-task-record');
            recordIdToDelete = button.getAttribute('data-id');

            const tasksToDelete = getTasksToDelete(recordIdToDelete);

            showDeleteWarningBootstrap(tasksToDelete, () => {

                // Sadece bunu çalıştırıyoruz ❗
                removeMainTaskAndDescendants(recordIdToDelete);
                updateTaskDashboardCards();
                showToast("Task deleted successfully!", "success");
            });
        }
    });

    function removeMainTaskAndDescendants(taskId) {

        const idsToDelete = getTasksToDelete(taskId);

        // dummyTaskData’dan sil
        idsToDelete.forEach(id => {
            const idx = dummyTaskData.findIndex(t => t.id === id.id);
            if (idx !== -1) dummyTaskData.splice(idx, 1);
        });

        // subTasks’tan sil
        idsToDelete.forEach(id => {
            const idx = subTasks.findIndex(st => st.id === id.id);
            if (idx !== -1) subTasks.splice(idx, 1);
        });

        // dependenciesTasks’tan sil
        idsToDelete.forEach(id => {
            const idx = dependenciesTasks.findIndex(st => st.id === id.id);
            if (idx !== -1) dependenciesTasks.splice(idx, 1);
        });

        // checklistTasks’tan sil
        idsToDelete.forEach(id => {
            const idx = checklistTasks.findIndex(st => st.id === id.id);
            if (idx !== -1) checklistTasks.splice(idx, 1);
        });

        // DataTable güncelle
        const tableElement = $('.workflow-task-list-table');
        if ($.fn.DataTable.isDataTable(tableElement)) {
            tableElement.DataTable()
                .clear()
                .rows.add(dummyTaskData)
                .draw();
        }
    }



    document.getElementById('confirmDeleteBtn').addEventListener('click', async function () {
        if (!recordIdToDelete) return;

        // Silinecek kaydın index’i
        const index = dummyTaskData.findIndex(item => item.id == recordIdToDelete);

        if (index !== -1) {
            dummyTaskData.splice(index, 1); // listeden kaldır

            // DataTable varsa yenile
            const tableElement = $('.workflow-task-list-table');
            if ($.fn.DataTable.isDataTable(tableElement)) {
                tableElement.DataTable().clear().rows.add(dummyTaskData).draw();
            }
        }

        bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();

        // Değişkenleri sıfırla
        recordIdToDelete = null;
        rowToDelete = null;
    });
}



async function loadWorkflow(workflowId) {
    try {
        const response = await fetch(`${window.ApiBaseUrl}/services/DitenPPM/Workflow/GetWorkflowById/${workflowId}`);
        if (!response.ok) throw new Error("API error");
        const result = await response.json();
        const data = result?.data;
        dummyData = data?.workFlowTeams || [];
        dummyTaskData = data?.workFlowTasks || [];

        populateSelect('ddl-status', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: data?.workflowStatusId || 0  // Tek kayıt varsa otomatik seçer

        });

        populateSelect('ddl-priority', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
            placeholder: 'Select priority',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: data?.priorityId || 0  // Tek kayıt varsa otomatik seçer

        });
        $('#txt-name').val(data?.name || "");
        $('#txt-code').val(data?.idCode || "");
        $('#txt-description').val(data?.description || "");
        const startLocal = toLocalDateOnly(data?.startDate);
        const endLocal = toLocalDateOnly(data?.endDate);

        document.getElementById("dtStartDate")._flatpickr.setDate(startLocal, true);
        document.getElementById("dtEndDate")._flatpickr.setDate(endLocal, true);
        
    } catch (err) {
        console.error(err);
        dummyData = []; // boş template
    }
}
function toLocalDateOnly(utcString) {
    const d = new Date(utcString);

    const pad = n => String(n).padStart(2, "0");

    return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()}`;
}
//-----TASKS---//
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
                            if (type === '1') return input.value.trim() !== '';
                            if (type === '2') return input.value.trim() !== '';
                            return true;
                        }
                    }
                }
            },
            taskCategory: {
                validators: {
                    callback: {
                        message: 'Category is required for the selected type',
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
                            return $('#add-task-type').val() !== '2' || $(input).val().length > 0;
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
                rowSelector: function (field, ele) {
                    return '.form-control-validation';
                }
            }),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });

    // Submit
    const btn = form.querySelector('.data-submit-add');
    btn.addEventListener('click', function () {
        fv.validate().then(function (status) {
            if (status === 'Valid') {

                const newId = generateObjectId([dummyTaskData, subTasks]);
                const taskName = $('#add-task-name').val();
                const taskTypeId = $('#add-task-type').val();
                const taskTypeName = $('#add-task-type').find('option:selected').text();
                const taskTypeCategoryId = $('#add-task-category').val();
                const taskTypeCategoryName = $('#add-task-category').find('option:selected').text();
                const taskDescription = $('#add-task-description').val();

                const ownerName = window.getUserName();
                const ownerId = window.getUserId();

                let assignee = [];
                let startDateTime = null;
                let endDateTime = null;
                let priorityId = 0;
                let priorityName = "";
                let statusId = 0;
                let statusName = "";
                let estimatedHour = 0;
                let dueDate = null;

                // 2. TaskType'a göre conditional atamalar
                if (taskTypeId === "1") { // Task
                    const selectedAssigneeId = $('#add-task-assignee').val();
                    assignee = selectedAssigneeId ? [{
                        id: selectedAssigneeId,
                        name: $('#add-task-assignee option[value="' + selectedAssigneeId + '"]').text()
                    }] : [];

                    // Start Date bugün
                    const today = new Date();
                    startDateTime = today;

                    // EndDate → dueDate
                    dueDate = document.getElementById("add-task-due-date").value;
                    endDateTime = dueDate ? new Date(dueDate) : null;

                    // Priority ve Status
                    priorityId = $('#add-task-priority').val();
                    priorityName = $('#add-task-priority').find('option:selected').text();
                    statusId = $('#add-task-status').val();
                    statusName = $('#add-task-status').find('option:selected').text();

                    estimatedHour = parseFloat(document.getElementById("add-task-estimated-hour").value) || 0;

                } else if (taskTypeId === "2") { // Meeting
                    const selectedAttendees = $('#add-meeting-attendees').val() || [];
                    assignee = selectedAttendees.map(id => ({
                        id: id,
                        name: $('#add-meeting-attendees option[value="' + id + '"]').text()
                    }));

                    // Start / End Datetime from flatpickr + select
                    const startDate = document.getElementById("add-meeting-start-date").value;
                    const startTime = document.getElementById("add-meeting-start-time").value;
                    startDateTime = getCombinedDateTime(startDate, startTime);

                    const endDate = document.getElementById("add-meeting-end-date").value;
                    const endTime = document.getElementById("add-meeting-end-time").value;
                    endDateTime = getCombinedDateTime(endDate, endTime);

                    // Priority / Status / EstimatedHour → reset
                    priorityId = 0;
                    priorityName = "";
                    statusId = 0;
                    statusName = "";
                    estimatedHour = 0;
                }


                // 3. Dummy data objesi
                const newTask = {
                    id: newId,
                    name: taskName,
                    typeId: taskTypeId,
                    typeName: taskTypeName,
                    categoryId: taskTypeCategoryId,
                    categoryName: taskTypeCategoryName,
                    description: taskDescription,
                    assignee: assignee,
                    startDateTime: startDateTime,
                    endDateTime: endDateTime,
                    priorityId: priorityId,
                    priorityName: priorityName,
                    statusId: statusId,
                    statusName: statusName,
                    estimatedHour: estimatedHour,
                    ownerId: ownerId,
                    ownerName: ownerName,
                    progress: "0%",
                    completedHour:0,
                };

                // 4. Dummy Data’ya Push
                dummyTaskData.push(newTask);
                updateTaskDashboardCards();
                dt_workflow_task.row.add(newTask).draw(false);
                $('#offcanvasCreateTask').offcanvas('hide');
                form.reset();


            } else {
                console.log("Form invalid!");
            }
        });
    });

    document.getElementById('offcanvasCreateTask')
        .addEventListener('hidden.bs.offcanvas', function () {
           
            //form.reset();
            //fv.resetForm(true); // FormValidation hatalarını tamamen temizler
        });

    document.querySelector('.data-full-form').addEventListener('click', function () {
        const taskTypeVal = $('#add-task-type').val();
        const btn = document.getElementById("btnCreateTask");
        btn.textContent = "Create Task";
        btn.setAttribute("data-mode", "create");
        btn.removeAttribute("data-edit-id");
        // Task type seçili değilse validation göster
        if (!taskTypeVal) {
            fv.validateField('taskType'); // hata mesajını gösterir
            return; // full form açılmasın
        }

        const recordType = urlParams.get('recordType') || '';
        // Seçili task type text
        const taskTypeText = $('#add-task-type').find('option:selected').text() || 'Task';
        document.getElementById('hdrTask').textContent = `New ${taskTypeText}`;

        document.getElementById('pTask').textContent =
            `Add a new ${taskTypeText} to your workspace and assign it to a ${recordType} and group`;
        handleTaskTabs(taskTypeVal);
        if (taskTypeVal === '1') toggleForms('taskFormContainer', 'meetingFormContainer');
        if (taskTypeVal === '2') toggleForms('meetingFormContainer', 'taskFormContainer');
        updateFormFields(taskTypeVal);
        document.getElementById('normalFormContainer').classList.add('d-none');
        document.getElementById('fullFormContainer').classList.remove('d-none');
        $('#offcanvasCreateTask').offcanvas('hide');
        //form.reset();

        
    });
}

document.getElementById('btnBackToNormal').addEventListener('click', function () {
    document.getElementById('fullFormContainer').classList.add('d-none');
    document.getElementById('normalFormContainer').classList.remove('d-none');
});
document.getElementById('btnCancelTask').addEventListener('click', function () {
    document.getElementById('fullFormContainer').classList.add('d-none');
    document.getElementById('normalFormContainer').classList.remove('d-none');
});
document.getElementById('btnCancelGeneral').addEventListener('click', function () {
    window.location.href = `/ppm/workflow-overview`;
});
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

function updateFormFields(taskTypeVal) {

    const addTaskNameEl = document.getElementById('add-task-name');
    const taskName = addTaskNameEl ? addTaskNameEl.value : '';

    const IntiativeEl = document.getElementById('txt-name');
    const intivativeName = IntiativeEl ? IntiativeEl.value : '';

    const addTaskDescriptionEl = document.getElementById('add-task-description');
    const taskDescription = addTaskDescriptionEl ? addTaskDescriptionEl.value : '';

    const addTaskCategoryEl = document.getElementById('add-task-category');
    const taskCategory = addTaskCategoryEl ? addTaskCategoryEl.value : '';

    ResetDependenciesTaskFormFields();
    ResetChecklistTaskFormFields();
    if (taskTypeVal === '1') {
        // TASK FORM ALANLARI AYARLARI

        $('#txt-task-name').val(taskName);       
        $('#txt-task-description').val(taskDescription);
        $('#txt-intiatives').val(intivativeName);

        const addTaskAssigneeEl = document.getElementById('add-task-assignee');
        const taskAssignee = addTaskAssigneeEl ? addTaskAssigneeEl.value : '';

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
        
        const addTaskStatusEl = document.getElementById('add-task-status');
        const taskStatus = addTaskStatusEl ? addTaskStatusEl.value : 0;

        populateSelect('ddl-task-status', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: taskStatus  

        });

        const addTaskPriorityEl = document.getElementById('add-task-priority');
        const taskPriority = addTaskPriorityEl ? addTaskPriorityEl.value : 0;

        populateSelect('ddl-task-priority', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
            placeholder: 'Select priority',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: taskPriority 

        });

        const EstimatedHEl = document.getElementById('add-task-estimated-hour');
        const estimatedHour = EstimatedHEl ? EstimatedHEl.value : '';
        $('#txt-task-estimated-hour').val(estimatedHour);

        // -------------------------------
        // MEETING FORM ALANLARINI RESETLE
        // -------------------------------
        $('#txt-meeting-name').val('');
        $('#txt-workflow').val('');
        $('#txt-meeting-description').val('');
        $('#checkVirtualMeeting').prop('checked', false);
        $('#txt-meeting-location').val('');
        $('#txt-meeting-link').val('');
        $('#ddl-meeting-attendees').val([]).trigger('change');
        $('#ddl-meeting-category').val(null).trigger('change');
        $('#txt-meeting-start-date').val('');
        $('#ddl-meeting-start-time').val(null).trigger('change');
        $('#txt-meeting-end-date').val('');
        $('#ddl-meeting-end-time').val(null).trigger('change');

        updateSubTaskFormFields();
        updateDependenciesTaskFormFields();
    }
    else if (taskTypeVal === '2') {
        
        // MEETING FORM ALANLARI AYARLARI
        $('#checkVirtualMeeting').prop('checked', false);
        $('#txt-meeting-location').val('');
        $('#txt-meeting-link').val('');

        // Checkbox duruma göre enable/disable ayarı
        if (!$('#checkVirtualMeeting').prop('checked')) {
            $('#txt-meeting-location').prop('disabled', false); // Location enable
            $('#txt-meeting-link').prop('disabled', true);      // Link disable
        }


        $('#txt-meeting-name').val(taskName);
        $('#txt-workflow').val(intivativeName);
        $('#txt-meeting-description').val(taskDescription);
        populateSelect('ddl-meeting-category', {
            apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskCategory`,
            placeholder: 'Select category',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: taskCategory

        });
        //add-meeting-start-time
        //add-meeting-end-time
        // Örnek kullanımı
        const mainStart = document.querySelector("#ddl-meeting-start-time");
        const mainEnd = document.querySelector("#ddl-meeting-end-time");
       
        // Eğer daha önce doldurulmuşsa tekrar doldurma
        if (mainStart.options.length === 0) {
            generateTimeOptions().forEach(opt => {
                mainStart.add(new Option(opt.text, opt.value));
                mainEnd.add(new Option(opt.text, opt.value));
            });
        }

        // Pending değer varsa uygula
        if (pendingAddToMainStart && hasOption(mainStart, pendingAddToMainStart)) {
            mainStart.value = pendingAddToMainStart;
            pendingAddToMainStart = null;
        }

        if (pendingAddToMainEnd && hasOption(mainEnd, pendingAddToMainEnd)) {
            mainEnd.value = pendingAddToMainEnd;
            pendingAddToMainEnd = null;
        }
        loadMainMeetingAttendees();

        // -----------------------------------------
        // TASK FORM ALANLARINI RESETLE
        // -----------------------------------------
        $('#txt-task-name').val('');
        $('#txt-intiatives').val('');
        $('#txt-task-description').val('');
        $('#ddl-task-assignee').val([]).trigger('change');
        $('#ddl-task-category').val(null).trigger('change');
        $('#ddl-task-status').val(null).trigger('change');
        $('#ddl-task-priority').val(null).trigger('change');
        $('#txt-task-estimated-hour').val('');
        $('#dtTaskStartDate').val('');
        $('#dtTaskDueDate').val('');

        ResetSubTaskFormFields();
        addAgendaItem();
    }
}

function updateSubTaskFormFields() {

    const IntiativeEl = document.getElementById('txt-name');
    const intivativeName = IntiativeEl ? IntiativeEl.value : '';
    $('#txt-sub-intiatives').val(intivativeName);

    populateSelect('ddl-sub-task-assignee', {
        data: dummyData,
        placeholder: 'Select assignee',
        valueKey: 'userId',
        textKey: 'fullName',
        autoSelectIfSingle: true

    });


    populateSelect('ddl-sub-task-category', {
        apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskCategory`,
        placeholder: 'Select category',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true

    });

    populateSelect('ddl-sub-task-status', {
        apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
        placeholder: 'Select status',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true

    });
    populateSelect('ddl-sub-task-priority', {
        apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`,
        placeholder: 'Select priority',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true

    });

    updateSubTaskMax();
}
txtTaskEstimated.addEventListener("input", updateSubTaskMax);
txtTaskEstimated.addEventListener("change", updateSubTaskMax);
// Sayfa yüklendiğinde sub task max = ana task süresi
function updateSubTaskMax() {
    const totalTaskMinutes = parseInt(txtTaskEstimated.value) || 0;

    // Tüm sub task estimatedHour toplamı
    const usedMinutes = subTasks.reduce((sum, st) => {
        return sum + (parseInt(st.estimatedHour) || 0);
    }, 0);

    const remainingMinutes = totalTaskMinutes - usedMinutes;

    subHourInput.max = remainingMinutes > 0 ? remainingMinutes : 0;
}
subHourInput.addEventListener("input", function () {
    const val = parseInt(this.value) || 0;
    const maxVal = parseInt(this.max) || 0;

    if (val > maxVal) {
        this.value = maxVal;
    }
});
function ResetSubTaskFormFields() {
    subTasks = [];
    $('#txt-sub-task-name').val('');
    $('#txt-sub-intiatives').val('');
    $('#txt-sub-task-description').val('');
    $('#ddl-sub-task-assignee').val([]).trigger('change');
    $('#ddl-sub-task-category').val(null).trigger('change');
    $('#ddl-sub-task-status').val(null).trigger('change');
    $('#ddl-sub-task-priority').val(null).trigger('change');
    $('#txt-sub-task-estimated-hour').val('');
    $('#dtSubTaskStartDate').val('');
    $('#dtDueTaskDueDate').val('');
    renderAllSubTasks();

}


// Create Sub Task butonu
const subTasksContainer = document.querySelector("#subTasksContainer");
function renderSubTaskCard(subTask, index) {
    const badgeClass = getPriorityBadgeClass(subTask);
    const assigneeName =
        subTask.assignee && subTask.assignee.length > 0
            ? subTask.assignee[0].name
            : "-";
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
            <p class="text-muted small mb-1">${subTask.mainTaskName || "-"}</p>
            <p class="text-muted extra-small mb-0"> <i class="bx bx-user"></i>
              ${assigneeName || "-"} <i class="bx bx-timer"></i> ${subTask.estimatedHour || 0} min
            </p>
          </div>

          <!-- Sağ delete butonu -->
          <div class="col-auto d-flex align-items-center">
            <a href="javascript:;" class="btn btn-icon delete-record"
   onclick="deleteSubTask('${subTask.id}')">
    <i class="icon-base bx bx-trash icon-md"></i>
</a>
          </div>

        </div>
      </div>
    </div>
    `;
    return cardHtml;
}

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
function addSubTask(subTask) {
    subTasks.push(subTask);
    renderAllSubTasks();
}
function renderAllSubTasks() {
    subTasksContainer.innerHTML = "";
    console.log("Rendering sub-tasks:", subTasks);
    subTasks.forEach((task, index) => {
        subTasksContainer.insertAdjacentHTML("beforeend", renderSubTaskCard(task, index));
    });
}
// Delete fonksiyonu
function deleteSubTask(id) {

    // 1️⃣ Silinecek tüm taskları bul
    const tasksToDelete = getTasksToDelete(id);

    // 2️⃣ Bootstrap modal ile uyarı göster
    showDeleteWarningBootstrap(tasksToDelete, () => {

        // 3️⃣ Onay gelirse SIL
        const index = subTasks.findIndex(st => st.id === id);
        if (index !== -1) {
            subTasks.splice(index, 1);
        }

        // DummyTaskData’dan ve diğer sub seviyelerinden tam temizleme
        removeTaskAndDescendants(id);

        // UI güncelle
        renderAllSubTasks();
        updateSubTaskMax();
    });
}

function getTasksToDelete(id) {
    const toDelete = [];

    function collect(taskId) {
        // Ana task
        const task = dummyTaskData.find(t => t.id === taskId);
        if (task) toDelete.push(task);

        // Alt taskları bul
        const children = dummyTaskData.filter(t => t.parentTaskId === taskId);
        for (const child of children) {
            collect(child.id); // recursion
        }
    }

    collect(id);

    return toDelete;
}

function showDeleteWarningBootstrap(tasks, onConfirm) {
    const listEl = document.getElementById("deleteTaskList");
    listEl.innerHTML = "";

    tasks.forEach(t => {
        listEl.innerHTML += `
            <li class="list-group-item">
                <i class="bx bx-chevron-right"></i> ${t.name}
            </li>
        `;
    });

    const modalEl = document.getElementById("deleteTaskModal");
    const modal = new bootstrap.Modal(modalEl);
    modal.show();

    document.getElementById("confirmTaskDelete").onclick = () => {
        modal.hide();
        onConfirm();
    };
}

function initializeSubTaskValidation() {

    const subTaskForm = document.getElementById("subTaskDetail");
    if (!subTaskForm) return;

    const fvSub = FormValidation.formValidation(subTaskForm, {
        fields: {
            txtSubTaskName: {
                validators: {
                    notEmpty: { message: "Name is required" },
                    stringLength: {
                        min: 3,
                        max: 250,
                        message: "Name must be between 3 and 250 characters"
                    }
                }
            },
            txtSubTaskDescription: {
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
            ddlSubTaskAssignee: {
                validators: {
                    notEmpty: { message: "Assignee is required" }
                }
            },
            ddlSubTaskCategory: {
                validators: {
                    notEmpty: { message: "Category is required" }
                }
            },
            ddlSubTaskStatus: {
                validators: {
                    notEmpty: { message: "Status is required" }
                }
            },
            ddlTaskPriority: {
                validators: {
                    notEmpty: { message: "Priority is required" }
                }
            },
            dtSubTaskStartDate: {
                validators: {
                    notEmpty: { message: "Start date is required" }
                }
            },
            dtSubTaskDueDate: {
                validators: {
                    notEmpty: { message: "Due date is required" }
                }
            },
            txtSubTaskEstimatedHour: {
                validators: {
                    notEmpty: { message: "Estimated hour is required" },
                    integer: { message: "Enter a valid number" },
                    greaterThan: {
                        inclusive: false,
                        min: 0,
                        message: "Estimated hour must be greater than 0"
                    }
                }
            }
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

    // ✨ Select2 değişince validation tetiklenmesi
    $("#ddl-sub-task-assignee, #ddl-sub-task-category, #ddl-sub-task-status, #ddl-sub-task-priority")
        .on("change", function () {
            fvSub.revalidateField(this.name);
        });

    return fvSub;
}




const btnCreateSubTask = document.getElementById("btnCreateSubTask");

btnCreateSubTask.replaceWith(btnCreateSubTask.cloneNode(true));

const newBtn = document.getElementById("btnCreateSubTask");
newBtn.addEventListener("click", function () {

    fvSub.validate().then(function (status) {

        if (status !== "Valid") {
            console.log("Sub Task formu geçersiz!");
            return;
        }
        const selectedAssigneeId = $('#ddl-sub-task-assignee').val();
        const subTask = {
            id: generateObjectId([dummyTaskData, subTasks]),
            name: document.querySelector("#txt-sub-task-name").value,
            description: document.querySelector("#txt-sub-task-description").value,
            typeId: 1, // sub task her zaman type 1 (task)
            typeName: "Task",
            parentTaskId: null, // ana task id'si sonradan atanacak
            mainTaskName: document.querySelector("#txt-task-name").value,
            assignee: selectedAssigneeId
                ? [{
                    id: selectedAssigneeId,
                    name: $('#ddl-task-assignee option[value="' + selectedAssigneeId + '"]').text()
                }]
                : [],
            priorityId: $("#ddl-sub-task-priority").val(),
            priorityName: $("#ddl-sub-task-priority option:selected").text(),
            categoryId: $("#ddl-sub-task-category").val(),
            categoryName: $("#ddl-sub-task-category option:selected").text(),
            statusId: $("#ddl-sub-task-status").val(),
            statusName: $("#ddl-sub-task-status option:selected").text(),
            estimatedHour: parseInt(document.querySelector("#txt-sub-task-estimated-hour").value),
            startDateTime: document.querySelector("#dtSubTaskStartDate").value,
            endDateTime: document.querySelector("#dtSubTaskDueDate").value,
        };

        addSubTask(subTask);

        // Form reset
        //fvSub.reset();
        $("#ddl-sub-task-assignee").val(null).trigger("change");
        $("#ddl-sub-task-category").val(null).trigger("change");
        $("#ddl-sub-task-status").val(null).trigger("change");
        $("#ddl-sub-task-priority").val(null).trigger("change");
        document.querySelector("#dtSubTaskStartDate")._flatpickr.clear();
        document.querySelector("#dtSubTaskDueDate")._flatpickr.clear();

        fvSub.resetForm(true);

        updateSubTaskMax();
    });
});

// yardımcı: bir select içinde option var mı kontrol et
function hasOption(selectEl, value) {
    return !!selectEl.querySelector(`option[value="${value}"]`);
}
function updateMainTime(value, type) {
    const mainStart = document.querySelector("#ddl-meeting-start-time");
    const mainEnd = document.querySelector("#ddl-meeting-end-time");

    if (type === "start") {
        if (hasOption(mainStart, value)) {
            mainStart.value = value;
            pendingAddToMainStart = null;
        } else {
            pendingAddToMainStart = value;
        }
    }

    if (type === "end") {
        if (hasOption(mainEnd, value)) {
            mainEnd.value = value;
            pendingAddToMainEnd = null;
        } else {
            pendingAddToMainEnd = value;
        }
    }
}
function filterDependenciesForTask(taskId, allTasks) {
    const task = allTasks.find(t => t.id === taskId);
    if (!task) return allTasks;

    const invalidIds = new Set([taskId]);

    // ------------------------------------------
    // 🔥 1) Recursive olarak TÜM subtasks'ları bul
    // ------------------------------------------
    function collectSubtasks(parentId) {
        const children = allTasks.filter(t => t.parentTaskId === parentId);

        children.forEach(child => {
            invalidIds.add(child.id);
            collectSubtasks(child.id); // 🔥 recursion
        });
    }

    collectSubtasks(taskId);

    // ------------------------------------------
    // 🔥 2) Parent zinciri (yukarı doğru recursive)
    // ------------------------------------------
    function collectParents(currentTask) {
        if (!currentTask || !currentTask.parentTaskId) return;

        const parent = allTasks.find(t => t.id === currentTask.parentTaskId);
        if (parent) {
            invalidIds.add(parent.id);
            collectParents(parent); // 🔥 recursion
        }
    }

    collectParents(task);

    // ------------------------------------------
    // 🚫 3) Filtreleme
    // ------------------------------------------
    return allTasks.filter(t => !invalidIds.has(t.id));
}

function updateDependenciesTaskFormFields() {


    populateSelect('ddl-dependencies-task', {
        data: dummyTaskData,
        placeholder: 'Select task',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true

    });



    populateSelect('ddl-dependencies-type', {
        apiUrl: `${window.ApiBaseUrl}/services/DitenPPM/Task/GetDependenciesType`,
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
            console.log("Sub Task formu geçersiz!");
            return;
        }
        const selectedDependencyId = $("#ddl-dependencies-task").val();
        const dependencyTask = dummyTaskData.find(t => t.id === selectedDependencyId);

        const dependenciesTask = {
            id: generateObjectId(dependenciesTasks),
            taskId: null,
            dependenciesTaskId: selectedDependencyId,
            dependenciesTaskName: $("#ddl-dependencies-task option:selected").text(),
            dependenciesTypeId: $("#ddl-dependencies-type").val(),
            dependenciesTypeName: $("#ddl-dependencies-type option:selected").text(),
            mainTaskName: document.querySelector("#txt-task-name").value,
            priorityId: dependencyTask?.priorityId ?? 0,
            priorityName: dependencyTask?.priorityName ?? "-",
            statusId: dependencyTask?.statusId ?? 0,
            statusName: dependencyTask?.statusName ?? "-",
            
        };

        addDependenciesTask(dependenciesTask);

        // Form reset
        //fvSub.reset();
        $("#ddl-dependencies-task").val(null).trigger("change");
        $("#ddl-dependencies-type").val(null).trigger("change");
        fvDependenciesTask.resetForm(true);

    });
});


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

let agendaCounter = 0;

// Attendees API yükleme
function loadAgendaAttendees(selectElement, done) {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;

    $.ajax({
        url: url,
        type: "GET",
        success: function (response) {
            if (!response || !response.data) {
                console.warn("Boş veya hatalı response:", response);
                return;
            }

            const users = response.data;

            // Selecti temizle
            $(selectElement).empty();

            // Kullanıcıları ekle
            users.forEach(user => {
                const option = new Option(user.fullName, user.id, false, false);
                selectElement.add(option);
            });

            $(selectElement).select2({
                dropdownParent: $(selectElement).closest(".agenda-item")
            });

            if (typeof done === "function") done();  // 🔥 CALLBACK ÇAĞRISI
        },
        error: function (err) {
            console.error("Attendees load error:", err);
        }
    });
}

document.getElementById("btnCreateAgenda").addEventListener("click", function () {
    addAgendaItem();
});


function addAgendaItem() {

    agendaCounter++;

    const container = document.getElementById("agendaDetailView");
    const template = document.getElementById("agenda-item-template");

    // Template içeriğini klonla
    const newItem = template.content.cloneNode(true).firstElementChild;

    newItem.querySelector(".agenda-number").textContent = agendaCounter;

    container.appendChild(newItem);

    // HTML elemanları
    const startDateInput = newItem.querySelector(".agenda-start-date");
    const endDateInput = newItem.querySelector(".agenda-end-date");
    const startTimeSelect = newItem.querySelector(".agenda-start-time");
    const endTimeSelect = newItem.querySelector(".agenda-end-time");
    const attendeesSelect = newItem.querySelector(".agenda-attendees");

    // MEETING TARİHLERİ
    const meetingStartDate = mainStartPicker.selectedDates[0];
    const meetingEndDate = mainEndPicker.selectedDates[0];

    const meetingStartTime = $("#ddl-meeting-start-time").val();
    const meetingEndTime = $("#ddl-meeting-end-time").val();

    // PREVIOUS END
    let prevEndDate = meetingStartDate;
    let prevEndTime = meetingStartTime;

    if (agendaCounter > 1) {
        const items = container.querySelectorAll(".agenda-item");
        const lastItem = items[items.length - 2];

        prevEndDate = lastItem.querySelector(".agenda-end-date")._flatpickr.selectedDates[0];
        prevEndTime = lastItem.querySelector(".agenda-end-time").value;
    }

    flatpickr(startDateInput, {
        dateFormat: "Y-m-d",
        defaultDate: prevEndDate,
        minDate: meetingStartDate,
        maxDate: meetingEndDate,
        static: true
    });

    flatpickr(endDateInput, {
        dateFormat: "Y-m-d",
        defaultDate: meetingEndDate,
        minDate: prevEndDate,
        maxDate: meetingEndDate,
        static: true
    });

    // Saatler
    generateTimeOptions().forEach(opt => {
        startTimeSelect.add(new Option(opt.text, opt.value));
        endTimeSelect.add(new Option(opt.text, opt.value));
    });

    // START TIME
    startTimeSelect.value = agendaCounter === 1 ? meetingStartTime : prevEndTime;

    // END TIME
    endTimeSelect.value = meetingEndTime;

    // Select2
    $(startTimeSelect).select2({ dropdownParent: $(newItem) });
    $(endTimeSelect).select2({ dropdownParent: $(newItem) });
    $(attendeesSelect).select2({ dropdownParent: $(newItem) });

    loadAgendaAttendees(attendeesSelect);

    // REMOVE
    newItem.querySelector(".btn-remove-agenda").addEventListener("click", function () {
        newItem.remove();
        reorderAgendaNumbers();
    });
}

// Numara sıralama fonksiyonu
function reorderAgendaNumbers() {
    const items = document.querySelectorAll("#agendaDetail .agenda-item");
    items.forEach((item, idx) => {
        item.querySelector(".agenda-number").textContent = idx + 1;
    });
}

function reorderAgendaNumbers() {
    const items = document.querySelectorAll("#agendaDetail .agenda-item");
    let num = 1;

    items.forEach(item => {
        item.querySelector(".agenda-number").textContent = num++;
    });

    agendaCounter = items.length;
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




//TASKS-----





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
function getCombinedDateTime(dateStr, timeStr) {
    if (!dateStr || !timeStr) return null;

    const [hour, minute] = timeStr.split(":").map(Number);

    const date = new Date(dateStr); // Flatpickr Y-m-d formatında verir
    date.setHours(hour);
    date.setMinutes(minute);
    date.setSeconds(0);
    date.setMilliseconds(0);

    return date;
}

document.getElementById("btnCreateTask")?.addEventListener("click", async function () {

    const mode = this.getAttribute("data-mode") || "create";
    const editId = this.getAttribute("data-edit-id");

    if (mode === "edit" && editId) {

        updateTask(editId);
    }
    else {

        createTask();

    }

    updateTaskDashboardCards();
    
    ResetChecklistTaskFormFields();
    ResetDependenciesTaskFormFields();
    ResetSubTaskFormFields();

    
    resetAgendaForm();
    // 2. Ortak alanlar
    document.getElementById('fullFormContainer').classList.add('d-none');
    document.getElementById('normalFormContainer').classList.remove('d-none');

});

function createTask() {

    // 1. ID üretimi (senin tarzında)
    const newId = generateObjectId([dummyTaskData, subTasks]);
    const taskTypeId = getTaskTypeFromTabs();
    const taskTypeName = taskTypeId === "1" ? "Task" : "Meeting";
    const ownerName = window.getUserName();
    const ownerId = window.getUserId();

    // 3. Değişkenler (Task/Meeting'e göre doldurulur)
    let name = null;
    let categoryId = "";
    let categoryName = "";
    let description = null;
    let assignee = [];
    let startDateTime = null;
    let endDateTime = null;
    let priorityId = 0;
    let priorityName = "";
    let statusId = 0;
    let statusName = "";
    let estimatedHour = 0;
    let dueDate = null;
    let progress = "0%";
    let agendaItems = [];
    let completedHour = 0;
    let location = "";
    let meetingLink = "";
    let checkVirtualMeetingLink = false;

    // 1️⃣ Önce subTasks içindeki her item'a parentTaskId ata
    const updatedSubTasks = subTasks.map(st => ({
        ...st,
        parentTaskId: newId   // burada parent id set ediliyor
    }));
    //dependenciesTasks
    const updatedDependenciesTask = dependenciesTasks.map(st => ({
        ...st,
        taskId: newId   // burada parent id set ediliyor
    }));
    if (!taskTypeId) { }
    else if (taskTypeId === "1") {

        name = $('#txt-task-name').val();
        categoryId = $('#ddl-task-category').val();
        categoryName = $('#ddl-task-category option:selected').text();
        description = $('#txt-task-description').val();
        const selectedAssigneeId = $('#ddl-task-assignee').val();
        assignee = selectedAssigneeId
            ? [{
                id: selectedAssigneeId,
                name: $('#ddl-task-assignee option[value="' + selectedAssigneeId + '"]').text()
            }]
            : [];
        statusId = $('#ddl-task-status').val();
        statusName = $('#ddl-task-status option:selected').text();
        priorityId = $('#ddl-task-priority').val();
        priorityName = $('#ddl-task-priority option:selected').text();
        estimatedHour = parseInt($('#txt-task-estimated-hour').val()) || 0;
        startDateTime = $("#dtTaskStartDate").val() ? new Date($("#dtTaskStartDate").val()) : new Date();
        endDateTime = $("#dtTaskDueDate").val() ? new Date($("#dtTaskDueDate").val()) : null;



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
        checkVirtualMeetingLink = $('#checkVirtualMeeting').is(':checked');
        const selectedAttendees = $('#ddl-meeting-attendees').val() || [];

        assignee = selectedAttendees.map(id => ({
            id: id,
            name: $('#ddl-meeting-attendees option[value="' + id + '"]').text()
        }));
        const startDate = $("#txt-meeting-start-date").val();
        const startTime = $("#ddl-meeting-start-time").val();
        const endDate = $("#txt-meeting-end-date").val();
        const endTime = $("#ddl-meeting-end-time").val();

        startDateTime = getCombinedDateTime(startDate, startTime);
        endDateTime = getCombinedDateTime(endDate, endTime);
        agendaItems = getAgendaItems();
    }

    const newTask = {
        id: newId,
        name: name,
        typeId: taskTypeId,
        typeName: taskTypeName,
        categoryId: categoryId,
        categoryName: categoryName,
        description: description,
        ownerId: ownerId,
        ownerName: ownerName,
        assignee: assignee,
        startDateTime: startDateTime,
        endDateTime: endDateTime,
        priorityId: priorityId,
        priorityName: priorityName,
        statusId: statusId,
        statusName: statusName,
        estimatedHour: estimatedHour,
        progress: progress,
        completedHour: completedHour,
        location: location,
        meetingLink: meetingLink,
        checkVirtualMeetingLink: checkVirtualMeetingLink,
        subTasks: updatedSubTasks,
        dependenciesTasks: updatedDependenciesTask,
        checklistTasks: checklistTasks,
        agendaItems: agendaItems
    };
    dummyTaskData.push(newTask);

    console.log("dummy Task Data:", dummyTaskData);
    console.log("New Task Created:", newTask);
    if (dt_workflow_task) {
        dt_workflow_task.row.add(newTask).draw(false);
        // 2️⃣ Sub taskları da ekle
        updatedSubTasks.forEach(st => {
            const normalized = normalizeSubTask(st, newTask);
            dummyTaskData.push(normalized);
            dt_workflow_task.row.add(normalized).draw(false);
        });
    }
    showToast("Task created successfully!", "success");

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
        isVirtual:checkVirtualMeetingLink,
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



function normalizeSubTask(st, parentTask) {
    return {
        ...st,
        location: parentTask.location || "",
        meetingLink: parentTask.meetingLink || "",
        checkVirtualMeetingLink: parentTask.checkVirtualMeetingLink || false,
        subTasks: [],                     // subtask'ın da subtask'ı olmaz
        dependenciesTasks: [],
        checklistTasks: [],
        agendaItems: [],
        progress: parentTask.progress || "0%",
        completedHour: parentTask.completedHour || 0,
        ownerId: parentTask.ownerId,
        ownerName: parentTask.ownerName,
        typeId: "1",                      // subTask = Task
        typeName: "Task",
        parentTaskId: parentTask.id
    };
}
function getAllDescendants(allTasks, parentId) {
    let result = [];

    // Bu parent’ın direkt çocuklarını bul
    const children = allTasks.filter(t => t.parentTaskId === parentId);

    for (const child of children) {
        result.push(child);
        result.push(...getAllDescendants(allTasks, child.id)); // recursion
    }

    return result;
}

function removeTaskAndDescendants(taskId) {

    // 1️⃣ O taskın tüm alt torunlarını bul
    const descendants = getAllDescendants(dummyTaskData, taskId);

    // 2️⃣ Silinecek tüm ID’ler
    const allRemoveIds = new Set([taskId, ...descendants.map(d => d.id)]);

    // 3️⃣ dummyTaskData’dan sil
    dummyTaskData = dummyTaskData.filter(t => !allRemoveIds.has(t.id));

    // 4️⃣ Diğer task’ların subTasks listesinden de sil
    dummyTaskData.forEach(task => {
        task.subTasks = task.subTasks.filter(st => !allRemoveIds.has(st.id));
    });
}

function resetAgendaForm() {
    const container = document.getElementById("agendaDetailView");

    // 1) Tüm gerçek agenda itemlarını temizle
    container.querySelectorAll(".agenda-item").forEach(item => item.remove());

    // 2) Sayaç sıfırlanmalı (addAgendaItem içinde ++ var)
    agendaCounter = 0;

 
}

function getAgendaItems() {
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
            startDateTime: getCombinedDateTime(startDate, startTime),
            endDateTime: getCombinedDateTime(endDate, endTime),
            title: agendaTitle,
            assignee: attendees.map(id => ({
                id: id,
                name: $('#ddl-meeting-attendees option[value="' + id + '"]').text()
            }))
        });
    });

    return items;
}

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

function updateTaskDashboardCards() {

    // Normalize task list (subTasks hariç)
    const tasks = dummyTaskData.filter(t => Number(t.typeId)!==2);

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

function updateTeamDashboardCards() {

    const team = dummyData || [];

    // 1️⃣ Total team resource
    const totalResource = team.length;

    // 2️⃣ Active members (teamStatus = true)
    const activeCount = team.filter(m => m.teamStatus === true).length;

    // 3️⃣ Total hours (sum of totalHour)
    const totalHours = team.reduce((sum, m) => sum + (Number(m.totalHour) || 0), 0);

    // 4️⃣ Average Allocation (%)
    //    (completedTask / totalTask) ortalaması
    let avgAllocation = 0;

    const validMembers = team.filter(m => m.totalTask > 0);
    if (validMembers.length > 0) {
        const totalRate = validMembers.reduce((sum, m) =>
            sum + (m.completedTask / m.totalTask), 0
        );
        avgAllocation = Math.round((totalRate / validMembers.length) * 100);
    }

    // 5️⃣ UI Update
    document.getElementById("teamTotalResource").textContent = totalResource;
    document.getElementById("teamActiveCount").textContent = `${activeCount} active`;
    document.getElementById("teamTotalHours").textContent = totalHours;
    document.getElementById("teamAvgAllocation").textContent = `${avgAllocation}%`;
}
function updateTeamDashboardCards() {

    const team = dummyData || [];

    // 1️⃣ Total team members
    const totalResource = team.length;

    // 2️⃣ Active members
    const activeCount = team.filter(m => m.teamStatus === true).length;

    // 3️⃣ Total Hours — tüm kullanıcıların task/subtask estimatedHour toplamı
    let totalHours = 0;

    let totalCompletedTasks = 0;
    let totalTasks = 0;

    team.forEach(member => {

        // Kullanıcı ID’si
        const userId = member.userId;

        // Bu kullanıcıya atanmış tüm task ve subtasklar
        const userTasks = dummyTaskData.filter(t =>
            Array.isArray(t.assignee) && !t.parentTaskId &&
            t.assignee.some(a => a.id == userId)
        );

        // Toplam hour
        totalHours += userTasks.reduce((sum, t) => sum + (Number(t.estimatedHour) || 0), 0);

        // Completed / total hesaplama
        totalTasks += userTasks.length;
        totalCompletedTasks += userTasks.filter(t => Number(t.statusId) === 3).length;
    });

    // 4️⃣ Average Allocation (%)
    const avgAllocation = totalTasks > 0
        ? Math.round((totalCompletedTasks / totalTasks) * 100)
        : 0;

    // 5️⃣ UI Update
    document.getElementById("teamTotalResource").textContent = totalResource;
    document.getElementById("teamActiveCount").textContent = `${activeCount} active`;
    document.getElementById("teamTotalHours").textContent = totalHours;
    document.getElementById("teamAvgAllocation").textContent = `${avgAllocation}%`;
}
function getUserTaskStats(userId) {

    // Bu kullanıcıya atanmış tüm task ve subtasklar
    const userTasks = dummyTaskData.filter(t =>
        Array.isArray(t.assignee) &&
        t.assignee.some(a => a.id === userId)
    );

    const total = userTasks.length;

    const completed = userTasks.filter(t => Number(t.statusId) === 3).length;

    return { total, completed };
}

function getUserTotalHours(userId) {

    if (!userId) return 0;

    // Bu kullanıcıya atanmış tüm task ve subtasklar
    const userTasks = dummyTaskData.filter(t =>
        Array.isArray(t.assignee) && !t.parentTaskId &&
        t.assignee.some(a => a.id === userId)
    );

    // estimatedHour toplama
    const totalHours = userTasks.reduce((sum, t) => {
        return sum + (Number(t.estimatedHour) || 0);
    }, 0);

    return totalHours;
}
