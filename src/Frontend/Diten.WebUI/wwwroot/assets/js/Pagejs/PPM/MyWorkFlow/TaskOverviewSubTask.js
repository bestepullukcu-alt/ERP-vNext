'use strict';


function getTaskById(id) {
    return window.taskOverviewList?.find(t => t.id === id);
}
function normalizeDate(d) {
    const date = new Date(d);
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

let fpSubStart, fpSubEnd;

function initSubtaskDatePickers(parentStart, parentEnd) {
    // Convert to Date objects
    if (fpSubStart) {
        fpSubStart.destroy();
        fpSubStart = null;
    }
    if (fpSubEnd) {
        fpSubEnd.destroy();
        fpSubEnd = null;
    }

    // Parent'tan sadece tarih kısmını al
    const min = normalizeDate(parentStart);
    const max = normalizeDate(parentEnd);

    // 1) START DATE PICKER
    fpSubStart = flatpickr("#mSubTaskStartDate", {
        enableTime: false,
        dateFormat: "d.m.Y",
        minDate: min,
        maxDate: max,
        allowInput: true,
        static: true,
        onChange: function (selectedDates) {
            if (selectedDates.length > 0) {
                const start = selectedDates[0];

                // Due date start’tan önceye düşemez
                fpSubEnd.set("minDate", start);

                // Eğer due date start’tan küçükse düzelt
                const endDate = fpSubEnd.selectedDates[0];
                if (endDate && endDate < start) {
                    fpSubEnd.setDate(start, true);
                }
            }
        }
    });

    // 2) DUE DATE PICKER
    fpSubEnd = flatpickr("#mSubTaskDueDate", {
        enableTime: false,
        dateFormat: "d.m.Y",
        minDate: min,
        maxDate: max,
        allowInput: true,
        static: true,
        onChange: function (selectedDates) {
            if (selectedDates.length > 0) {
                const due = selectedDates[0];

                // Start date due’dan büyük olamaz
                fpSubStart.set("maxDate", due);

                const startDate = fpSubStart.selectedDates[0];
                if (startDate && startDate > due) {
                    fpSubStart.setDate(due, true);
                }
            }
        }
    });
}
function calculateRemainingEstimated(parentTaskId) {
    const parent = getTaskById(parentTaskId);
    if (!parent) return 0;

    const parentEstimated = parent.estimatedHour || 0;

    // Bu parent’a bağlı diğer subtasklar
    const subTasks = window.taskOverviewList.filter(t => t.parentTaskId === parentTaskId);

    const used = subTasks.reduce((sum, s) => sum + (s.estimatedHour || 0), 0);

    return Math.max(parentEstimated - used, 0); // kalan dakika
}
$(document).on("click", ".add-subtask", function () {
    const taskId = $(this).data("id");
    const name = $(this).data("name");
    const description = $(this).data("name");
    const parent = getTaskById(taskId);
    if (!parent) return;
    window.parentStartDate = parent.startDate;
    window.parentDueDate = parent.endDate;
    // Seçilen task'ı global state'e kaydet
    window.selectedParentTaskId = taskId;

    document.getElementById("hSubTaskNameModal").textContent =
        `Create task for / ${name}`;
    document.getElementById("pSubTaskDescriptionModal").textContent = description;
    loadAssignees();
    loadCategories();
    loadStatus();
    loadPriority();
    initSubtaskDatePickers(parent.startDate, parent.endDate);
    const remaining = calculateRemainingEstimated(taskId);
    const input = document.getElementById("m-sub-task-estimated-hour");
    input.setAttribute("max", remaining);
    input.setAttribute("min", 0);
    input.value = "";
    input.placeholder = `Max ${remaining} minutes`;
    // Modal aç

    $('#m-sub-task-assignee').val(null).trigger("change");
    $('#m-sub-task-category').val(null).trigger("change");
    $('#m-sub-task-status').val(null).trigger("change");
    $('#m-sub-task-priority').val(null).trigger("change");

    // Tarih alanlarını temizle (flatpickr)
    if (window.fpSubStart) fpSubStart.clear();
    if (window.fpSubEnd) fpSubEnd.clear();

    // FormValidation state reset
    if (window.subTaskFv) {
        window.subTaskFv.resetForm(true);  // ✔ tüm kırmızı/yeşilleri ve mesajları temizler
    }

    const modal = new bootstrap.Modal(document.getElementById("addSubTaskModal"));
    modal.show();

    if (window.subTaskFv) {
        window.subTaskFv.resetForm(true);
    }
    initializeCreateSubTaskValidation();

    // İstersen modal içini dolduralım:
    //fillSubTaskModal(taskId);
});



document.getElementById("m-sub-task-estimated-hour")
    .addEventListener("input", function () {

        const max = Number(this.getAttribute("max"));
        let val = Number(this.value);

        if (val > max) {
            this.value = max;
            showToast(`Estimated hour cannot exceed ${max} minutes.`, "warning");
        }

        if (val < 0) {
            this.value = 0;
        }
    });

async function loadAssignees() {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const response = await res.json();

    initSelect2($('#m-sub-task-assignee'), response.data, "id", "fullName");
}

async function loadCategories() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskCategory`;
    const res = await fetch(url);
    const data = await res.json();

    if (!Array.isArray(data)) return;

    initSelect2($('#m-sub-task-category'), data, "id", "name");
}
async function loadStatus() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`;
    const res = await fetch(url);
    const data = await res.json();

    if (!Array.isArray(data)) return;

    initSelect2($('#m-sub-task-status'), data, "id", "name");
}
async function loadPriority() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetPriorities`;
    const res = await fetch(url);
    const data = await res.json();

    if (!Array.isArray(data)) return;

    initSelect2($('#m-sub-task-priority'), data, "id", "name");
}
function initSelect2($el, list, valueKey = "id", textKey = "name") {
    $el.empty();

    // Placeholder için boş option ekleyelim
    $el.append(`<option></option>`);

    list.forEach(item => {
        $el.append(
            `<option value="${item[valueKey]}">${item[textKey]}</option>`
        );
    });

    $el.val(null).trigger("change");
}

let subTaskFv = null;
function initializeCreateSubTaskValidation() {

    const form = document.getElementById('createSubTaskFormModal');
    if (!form) return;

    if (window.subTaskFv) return;

    window.subTaskFv = FormValidation.formValidation(form, {
        fields: {

            // ✔ Subtask Name
            mSubName: {
                validators: {
                    notEmpty: { message: 'Name is required' },
                    stringLength: {
                        min: 3,
                        message: 'Name must be at least 3 characters'
                    }
                }
            },

            // ✔ Description
            mDescription: {
                validators: {
                    notEmpty: { message: 'Description is required' }
                }
            },

            // ✔ Assignee
            mSubTaskAssignee: {
                validators: {
                    notEmpty: { message: 'Assignee is required' }
                }
            },

            // ✔ Category
            mSubTaskCategory: {
                validators: {
                    notEmpty: { message: 'Category is required' }
                }
            },

            // ✔ Status
            mSubTaskStatus: {
                validators: {
                    notEmpty: { message: 'Status is required' }
                }
            },

            // ✔ Priority
            mTaskPriority: {
                validators: {
                    notEmpty: { message: 'Priority is required' }
                }
            },

            // ✔ Start Date
            mSubTaskStartDate: {
                validators: {
                    notEmpty: { message: 'Start Date is required' }
                }
            },

            // ✔ Due Date
            mSubTaskDueDate: {
                validators: {
                    notEmpty: { message: 'Due Date is required' }
                }
            },

            // ✔ Estimated Hour
            mSubTaskEstimatedHour: {
                validators: {
                    notEmpty: { message: 'Estimated hour is required' },
                    greaterThan: {
                        min: 1,
                        message: 'Estimated hour must be greater than 0'
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

    // ---------- SUBMIT ----------
    const btn = form.querySelector('.data-submit-add-subtask');

    btn.addEventListener('click', function () {
        window.subTaskFv.validate().then(async function (status) {
            if (status !== 'Valid') {
                console.log("Subtask form invalid");
                return;
            }

            const payload = buildSubTaskPayload();
            console.log("Gönderilen Subtask:", payload);

            await submitCreateSubTask(payload);
        });
    });
}

function buildSubTaskPayload() {

    const parentId = window.selectedParentTaskId;
    const parentTask = getTaskById(parentId);

    return {
        parentTaskId: parentId,
        workflowId: parentTask.workFlowId,
        name: $('#m-sub-name').val().trim(),
        description: $('#m-description').val().trim(),
        assigneeId: $('#m-sub-task-assignee').val(),
        categoryId: $('#m-sub-task-category').val(),
        statusId: $('#m-sub-task-status').val(),
        priorityId: $('#m-sub-task-priority').val(),
        startDate: toIsoDate($('#mSubTaskStartDate').val()),
        dueDate: toIsoDate($('#mSubTaskDueDate').val()),
        estimatedHour: Number($('#m-sub-task-estimated-hour').val() || 0),
        ownerId: window.getUserId(),
        createdBy: window.getUserName()
    };
}
function toIsoDate(dateStr) {
    if (!dateStr) return null;

    const parts = dateStr.split(".");
    if (parts.length !== 3) return null;

    const day = parts[0];
    const month = parts[1];
    const year = parts[2];

    // ISO: YYYY-MM-DDT00:00:00Z
    return `${year}-${month}-${day}T00:00:00Z`;
}
async function submitCreateSubTask(payload) {
    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/CreateSubTask`;

        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const json = await res.json();

        if (json.errors != null && json.errors.length>0) {
            showToast(json.errors || "Subtask could not be created.", "error");
            return;
        }

        // ✔ SUCCESS
        showToast("Subtask created successfully.");
        await refreshTaskOverview();
        // Modalı kapat
        const modal = bootstrap.Modal.getInstance(document.getElementById("addSubTaskModal"));
        modal.hide();

        //document.getElementById("createSubTaskFormModal").reset();


        // Task list refresh
        /*await loadTaskOverviewData();*/

    } catch (err) {
        console.error(err);
        showToast("An unexpected error occurred.", "error");
    }
}
