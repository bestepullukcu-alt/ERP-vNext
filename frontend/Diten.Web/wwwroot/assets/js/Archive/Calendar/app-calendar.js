'use strict';
const eventGuests = $('#eventGuests');
const port2 = protocol === 'https:' ? '5055' : '5050';
const port3 = protocol === 'https:' ? '5060' : '5053';
const userId = getUserId();
const userName = window.getUserName;

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

async function loadUsers() {
    try {
        const apiUrl = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByUserId/${userId}`;
        console.log("API URL:", apiUrl);
        const response = await fetch(apiUrl);
        if (!response.ok) throw new Error("HTTP hatası: " + response.status);

        const result = await response.json();  // JSON nesnesini al
        const guests = result.data || [];      // data dizisini çıkar

        eventGuests.empty();
        guests.forEach(g => {
            // Option’un value’su id, görünen metin fullName olacak
            const option = new Option(g.fullName, g.id, false, false);
            $(option).attr('data-avatar', g.avatar ?? 'default.png');
            if (g.id === userId) {
                $(option).prop('selected', true);
            }
            eventGuests.append(option);
        });

        eventGuests.wrap('<div class="position-relative"></div>').select2({
            placeholder: 'Select guests',
            dropdownParent: eventGuests.parent(),
            closeOnSelect: false,
            //templateResult: renderGuestAvatar,
            //templateSelection: renderGuestAvatar,
            escapeMarkup: es => es
        });
    } catch (error) {
        console.error('Guest listesi yüklenemedi:', error);
    }
}


//--------------------------------- CALENDAR SIDEBAR TASKS ---------------------------------//
let calendarDefaultFilter = {
    currentUserId: window.getUserId(),
    dueDateFilter: null,            // delayed | today | nextWeek | nextMonth
    priorityIds: null,              // [1,2,3,4]
    assignedFromUserIds: null       // [ownerId]
};
async function fetchCalendarSidebarData(filter = calendarDefaultFilter) {

    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/CalendarSidebar`;
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({filter})
    });

    const json = await response.json();

    return {
        tasks: json.data?.tasks || [],
        meetings: json.data?.meetings || []
    };
}
document.getElementById("taskSort").addEventListener("change", async function () {
    const sortType = this.value; // priority | status | date

    // mevcut filtreyi al
    const data = await fetchCalendarSidebarData(calendarDefaultFilter);

    const sortedTasks = sortTasks(data.tasks, sortType);
    const sortedMeetings = sortTasks(data.meetings, sortType);

    renderSidebarTasks(sortedTasks);
    // ❗ burada sidebar’ı yeniden render etmelisin
    renderSidebarMeetings(sortedMeetings);
});
function sortTasks(list, sortType) {

    if (!Array.isArray(list)) return [];

    return list.slice().sort((a, b) => {

        switch (sortType) {

            case "priority":
                return (a.priorityId || 999) - (b.priorityId || 999);

            case "status":
                return (a.statusId || 999) - (b.statusId || 999);

            case "date":
                const dateA = new Date(a.startDateTime || a.dueDate || a.date || "2100-01-01");
                const dateB = new Date(b.startDateTime || b.dueDate || b.date || "2100-01-01");
                return dateA - dateB;

            default:
                return 0;
        }
    });
}

function renderSidebarMeetings(meetings) {
    const container = document.getElementById("myMeetingForm");
    container.innerHTML = ""; // temizle

    meetings.forEach(m => {

        const alertClass = getMeetingStatusBadgeClass(m.statusId);
        const dateHtml = getMeetingDateHtml(m.startDate, m.endDate);
        const timeHtml = getMeetingTimeHtml(m.startDate, m.endDate);
        const div = document.createElement("div");
        div.className = `
            ${alertClass}
            mb-2
            pointer
            p-3
            rounded
        `;

        // Drag-drop classları OLMAYACAK
        // fc-event fc-daygrid-event vs. YOK

        div.dataset.meetingId = m.id;


        div.innerHTML = `
            <div class="fw-bold">${m.name}</div>

            <div class="mt-2 small text-muted">
                <i class="bx bx-user me-1"></i>
                ${m.ownerName}
            </div>

            <div class="mt-2 small text-muted">
                ${dateHtml}
            </div>

            <div class="mt-1 small text-muted">
                ${timeHtml}
            </div>

            <div class="mt-2 small text-muted">
                <i class="bx bx-map me-1"></i>
                ${m.location || "-"}
            </div>
        `;
        div.addEventListener("click", () => openMeetingDetailModal(m.id));
        div.onclick = () => openMeetingDetailModal(m.id);
        container.appendChild(div);
    });
}
async function openMeetingDetailModal(meetingId) {
    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/GetMeetingDetail/${meetingId}`;

    const response = await fetch(url, {
        method: "GET",
        headers: { "Content-Type": "application/json" }
    });

    const data = await response.json();
    const m = data.data;

    // SET FIELDS
    document.getElementById("mdTitle").innerText = m.name;
    document.getElementById("mdDescription").innerText = m.description || "";
    document.getElementById("mdOwnerName").innerText = m.ownerName;

    document.getElementById("mdDate").innerText =
        moment(m.startDate).format("DD.MM.YYYY");

    document.getElementById("mdTime").innerText =
        moment(m.startDate).format("HH:mm") +
        " - " +
        moment(m.endDate).format("HH:mm");

    document.getElementById("mdLocation").innerText =
        m.location || "-";

    document.getElementById("mdLink").href =
        m.meetingLink || "#";

    // ATTENDEES
    document.getElementById("mdAttendeeCount").innerText =
        m.attendees.length;

    const attContainer = document.getElementById("mdAttendees");
    attContainer.innerHTML = "";

    m.attendees.forEach(a => {
        attContainer.innerHTML += `
            <span class="badge badge bg-label-secondary border">${a.name}</span>
        `;
    });

    // AGENDA
    const agendaContainer = document.getElementById("mdAgenda");
    agendaContainer.innerHTML = "";

    m.meetingAgendas.forEach((ag, idx) => {
        agendaContainer.innerHTML += `
    <div class="agenda-item p-3 mb-3 rounded border bg-body">

        <div class="d-flex align-items-center">

            <!-- LEFT: NUMBER BADGE -->
            <div class="d-flex justify-content-center align-items-center me-3" style="min-width: 50px;">
                <span class="agenda-number badge bg-primary rounded-pill"
                      style="font-size: 14px; padding: 8px 14px;">
                    ${idx + 1}
                </span>
            </div>

            <!-- CENTER: TITLE + ASSIGNEES -->
            <div class="flex-grow-1 d-flex flex-column">

                <!-- TITLE -->
                <div class="fw-medium">
                    ${ag.title}
                </div>

                <!-- ASSIGNEES -->
                <div class="mt-1 small d-flex flex-wrap gap-2">
                    ${ag.assignee
            .map(p => `<span class="badge bg-label-secondary border">${p.name}</span>`)
                .join("")}
                </div>

            </div>

            <!-- RIGHT: DURATION (VERTICALLY CENTERED) -->
            <div class="d-flex justify-content-end align-items-center ms-3 text-primary"
                 style="min-width:70px; white-space:nowrap;font-size:16px">
                ${calculateDuration(ag.startDateTime, ag.endDateTime)}
            </div>

        </div>

    </div>
`;



    });
    document.getElementById("btnAccept").onclick = () => updateMeetingStatus(meetingId, 2);
    document.getElementById("btnMaybe").onclick = () => updateMeetingStatus(meetingId, 3);
    document.getElementById("btnDecline").onclick = () => updateMeetingStatus(meetingId, 4);
    document.getElementById("btnAccept").onclick = () => updateMeetingStatus(meetingId, 2);
    document.getElementById("btnMaybe").onclick = () => updateMeetingStatus(meetingId, 3);
    document.getElementById("btnDecline").onclick = () => updateMeetingStatus(meetingId, 4);
    // Aç modal
    const modal = new bootstrap.Modal(document.getElementById("meetingDetailModal"));
    modal.show();
}
function calculateDuration(start, end) {
    const s = moment(start);
    const e = moment(end);

    const diffMinutes = e.diff(s, "minutes");
    if (diffMinutes <= 0) return "0m";

    const days = Math.floor(diffMinutes / 1440);     // 1440 = 24*60
    const hours = Math.floor((diffMinutes % 1440) / 60);
    const minutes = diffMinutes % 60;

    let out = "";

    if (days > 0) out += `${days}d `;
    if (hours > 0) out += `${hours}h `;
    if (minutes > 0) out += `${minutes}m`;

    return out.trim();
}



async function updateMeetingStatus(meetingId, statusId) {
    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/RespondMeeting`;

    const payload = {
        statusId: statusId,
        taskId: meetingId,
        userId: window.getUserId(),
        CreatedBy: window.getUserName()
        
    };
    try {
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        //if (!res.ok) {
        //    showToast("Something went wrong", "error");
        //    return;
        //}

        // ✔ Success toast
        showToast("Your response has been saved", "success");

        // ✔ Modal'ı kapat
        const modalEl = document.getElementById("meetingDetailModal");
        if (modalEl) {
            const modalInstance = bootstrap.Modal.getInstance(modalEl);
            if (modalInstance) modalInstance.hide();
        }
        // ✔ Backdrop’ı zorla temizle
        setTimeout(() => {
            document.body.classList.remove("modal-open");
            document.body.style.overflow = "auto";

            document.querySelectorAll(".modal-backdrop")
                .forEach(el => el.remove());
        }, 150);
        // ✔ Takvimi yenile
        if (typeof calendar !== "undefined") {
            calendar.refetchEvents();
        }

        // ✔ Sidebar'ı yenile
        if (typeof loadCalendarSidebar === "function") {
            loadCalendarSidebar();
        }

    } catch (err) {
        console.error("Meeting update error:", err);
        showToast("Error while updating meeting", "error");
    }
}

function getMeetingDateHtml(start, end) {
    if (!start) return "-";

    const sDate = moment(start).format("DD MMM");
    const eDate = moment(end).format("DD MMM");

    // Aynı günse TEK SATIR
    if (moment(start).isSame(end, "day")) {
        return `
            <i class="bx bx-calendar me-1"></i>
            ${sDate}
        `;
    }

    // Farklı günse İKİ SATIR
    return `
        <div>
            <i class="bx bx-calendar me-1"></i>
            Start: ${sDate}
        </div>
        <div class="ms-4">
            <i class="bx bx-calendar"></i>
            End: ${eDate}
        </div>
    `;
}
function getMeetingTimeHtml(start, end) {
    if (!start) return "";

    const sTime = moment(start).format("HH:mm");
    const eTime = moment(end).format("HH:mm");

    return `
        <i class="bx bx-time-five me-1"></i>
        ${sTime} - ${eTime}
    `;
}

function getMeetingStatusBadgeClass(statusId) {
    switch (statusId) {
        case 4: return "alert alert-danger";   // Critical
        case 3: return "alert alert-warning";  // High
        case 2: return "alert alert-success";     // Medium
        case 1: return "alert alert-primary";  // Low
        default: return "alert alert-secondary";
    }
}

function formatMeetingDate(start, end) {
    if (!start) return "-";

    const s = moment(start).format("DD MMM, HH:mm");
    const e = moment(end).format("DD MMM, HH:mm");

    return `${s} → ${e}`;
}
function renderSidebarTasks(tasks) {
    const container = document.getElementById("external-tasks");
    container.innerHTML = ""; // temizle

    tasks.forEach(t => {

        const alertClass = getPriorityClass(t.priorityId, t.endDate);
        const statusClass = getStatusBadgeClass(t.statusId);
        const div = document.createElement("div");
        div.className = `
            ${alertClass}
            fc-event
            fc-h-event
            fc-daygrid-event
            fc-daygrid-block-event
            mb-2
            p-2
            pointer
        `;

        div.dataset.taskId = t.id;
        div.dataset.event = JSON.stringify({
            id: t.id,
            title: t.name,
            end: t.endDate
        });

        // FINAL CARD HTML
        div.innerHTML = `
    <div><strong class="task-title"">${t.name}</strong></div>

    <!-- ALT SATIR -->
    <div class="d-flex justify-content-between align-items-center mt-0">

        <!-- SOL TARAF -->
        <div class="d-flex align-items-center small text-muted">
            <i class="bx bx-calendar me-1"></i>
            <span>${formatDateYMD(t.endDate)}</span>

            <i class="bx bx-user me-1"></i>
            <span class="me-3 small">${t.ownerName}</span>
        </div>

        <!-- SAĞ TARAF -->
        
    </div>
`;


        container.appendChild(div);
    });
}

function formatDateYMD(dateStr) {
    if (!dateStr) return "-";

    const d = new Date(dateStr);
    if (isNaN(d)) return dateStr;

    const day = d.getDate().toString().padStart(2, "0");
    const month = (d.getMonth() + 1).toString().padStart(2, "0");
    const year = d.getFullYear();

    return `${year}-${month}-${day}`;
}

function getStatusBadgeClass(statusId) {
    const ntStatusId = parseInt(statusId);
    switch (ntStatusId) {
        case 1: return "text-bg-secondary"; // To Do
        case 2: return "text-bg-primary";   // In Progress
        default: return "text-bg-dark"; // Diğerleri için fallback
    }
}
function getPriorityClass(priorityId, endDate) {
    // 🔥 Due date geçti mi?
    if (endDate) {
        const due = new Date(endDate);
        const today = new Date();

        if (due < today) {
            return "alert alert-danger"; // overdue → kırmızı
        }
    }

    // 🔽 Normal priority renkleri
    switch (priorityId) {
        case 4: return "alert alert-secondary";   // Critical
        case 3: return "alert alert-secondary";  // High
        case 2: return "alert alert-secondary";     // Medium
        case 1: return "alert alert-secondary";  // Low
        default: return "alert alert-secondary";
    }
}


function initTaskDraggables() {
    new FullCalendar.Draggable(document.getElementById("external-tasks"), {
        itemSelector: ".fc-event",
        eventData: function (eventEl) {
            return {
                title: eventEl.innerText.trim(),
                backgroundColor: window.getComputedStyle(eventEl).backgroundColor,
                borderColor: window.getComputedStyle(eventEl).backgroundColor,
                textColor: window.getComputedStyle(eventEl).color
            };
        }

        //new FullCalendar.Draggable(containerEl, {
        //    itemSelector: '.fc-event',
        //    eventData: function (eventEl) {
        //        return {
        //            title: eventEl.innerText.trim(),
        //            backgroundColor: window.getComputedStyle(eventEl).backgroundColor,
        //            borderColor: window.getComputedStyle(eventEl).backgroundColor,
        //            textColor: 'white'
        //        };
        //    }
        //});


    });
}
async function loadCalendarSidebar() {
    const { tasks, meetings } = await fetchCalendarSidebarData();
    renderSidebarTasks(tasks);
    renderSidebarMeetings(meetings);
    initTaskDraggables();
}
//--------------------------------- END CALENDAR SIDEBAR TASKS ---------------------------------//

//----------------------------------- ADD EVENT MODAL -----------------------------------//
async function loadCreateTaskTypes() {
    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskTypes`;
    const res = await fetch(url);
    const list = await res.json();

    const $el = $('#ddlType');

    // Eski select2 instance varsa önce destroy et
    if ($el.hasClass("select2-hidden-accessible")) {
        $el.select2("destroy");
    }

    $el.empty();

    list.forEach(x => {
        $el.append(`<option value="${x.id}">${x.name}</option>`);
    });

 
}
async function loadCreateTaskStatus() {
    const url = `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`;
    const res = await fetch(url);
    const list = await res.json();

    const $el = $('#ddlStatus');

    // Eski select2 instance varsa önce destroy et
    if ($el.hasClass("select2-hidden-accessible")) {
        $el.select2("destroy");
    }

    $el.empty();

    list.forEach(x => {
        $el.append(`<option value="${x.id}">${x.name}</option>`);
    });


}
async function loadCreateTaskPriority() {
    const url = `${window.ApiBaseUrl}/services/DitenPPM/WorkflowCategory/GetPriorities`;
    const res = await fetch(url);
    const list = await res.json();

    const $el = $('#ddlPriority');

    // Eski select2 instance varsa önce destroy et
    if ($el.hasClass("select2-hidden-accessible")) {
        $el.select2("destroy");
    }

    $el.empty();

    list.forEach(x => {
        $el.append(`<option value="${x.id}">${x.name}</option>`);
    });


}

async function loadCreateTaskCategories() {
    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskCategory`;
    const res = await fetch(url);
    const result = await res.json();

    const $el = $('#ddlCategory');
    $el.empty();

    result.forEach(x => {
        $el.append(`<option value="${x.id}">${x.name}</option>`);
    });

   
}
async function loadCreateTaskWorkflows() {
    const url = `${window.ApiBaseUrl}/services/DitenPPM/Workflow/GetWorkflows`;
    const res = await fetch(url);
    const result = await res.json();

    const $el = $('#ddlWorkflow');
    $el.empty();

    // ---- PLACEHOLDER EKLE (Seçili olmasın) ----
    $el.append(`<option value="" disabled selected>Select workflow...</option>`);

    result.data.forEach(item => {
        $el.append(`<option value="${item.id}">${item.name}</option>`);
    });
    // Eğer Select2 kullanıyorsan refresh et
    if ($el.hasClass("select2-hidden-accessible")) {
        $el.trigger("change.select2");
    }
}
async function loadCreateTaskAssignee() {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const list = await res.json();

    const $assignee = $('#ddlAssignee');

    $assignee.empty();

    // ---- PLACEHOLDER EKLE (Seçili olmasını engeller) ----
    $assignee.append(`<option value="" disabled selected>Select assignee...</option>`);

    list.data.forEach(u => {
        const option = `<option value="${u.id}">${u.fullName}</option>`;
        $assignee.append(option);
    });

    // ---- Select2 refresh ----
    if ($assignee.hasClass("select2-hidden-accessible")) {
        $assignee.trigger("change.select2");
    }
}
async function loadCreateTaskAttendees() {
    const url = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const list = await res.json();

    const $attendees = $('#ddlAttendees');

    $attendees.empty();



    list.data.forEach(u => {
        const option = `<option value="${u.id}">${u.fullName}</option>`;
        $attendees.append(option);
    });

    // 3) select2 refresh
    $attendees.trigger("change");

}
let createTaskFormInitialized = false;
$(document).on("click", ".btn-create-task", async function () {
    isEditMode = false;
    isPopulatingSchedule = false;
    //selectedTaskId = null;
    $("#editUser .modal-title").text("New Task / Meeting");
    console.log("🟦 CREATE mode activated");
    $(".createTask")
        .attr("data-mode", "create")
        .removeAttr("data-id")
        .text("Save");
    // Formu temizle
    //clearFormForCreate();

    // Create form initialization  (bir kez çalışır)
   /* if (!createTaskFormInitialized) {*/
        console.log("🔧 initCreateTaskForm çalışıyor...");
        await initCreateTaskForm();
        //createTaskFormInitialized = true;
   /* }*/

    // Schedule system her seferinde çalışmalı
    initScheduleSystem();

    // Modalı aç
    const modal = new bootstrap.Modal(document.getElementById("editUser"));
    modal.show();
});

//$('#editUser').on('shown.bs.modal', async function () {
//    initScheduleSystem();

//    if (createTaskFormInitialized) {
//        console.log("⚠ createTaskForm zaten init edildi, tekrar çalıştırılmayacak.");
//        return;
//    }

//    console.log("✅ editUser modal shown → initCreateTaskForm çalışıyor...");
//    await initCreateTaskForm();
//    createTaskFormInitialized = true;
//});

async function initCreateTaskForm() {
    console.log("⚙ initCreateTaskForm başladı...");

    // 1) API'den verileri çek
    await Promise.all([
        loadCreateTaskTypes(),
        loadCreateTaskCategories(),
        loadCreateTaskWorkflows(),
        loadCreateTaskStatus(),
        loadCreateTaskPriority(),
        fillMeetingTimes()
    ]);

    console.log("✔ Dropdown verileri yüklendi, select2 init ediliyor...");

    // 2) Select2 init (SADECE BURADA)
    $('#editUser .select2').select2({
        dropdownParent: $('#editUser'),
        width: '100%'
    });

    // 3) Type change event
    $('#ddlType').on('change', function () {
        const val = $(this).val();
        console.log("🔄 Type changed:", val);
        toggleTaskMeetingFields(val);
    });

    initAllSelect2();
    initTaskDatePickers();
    initMeetingPickers();
    // 4) İlk değer için trigger
    $('#ddlType').val(null).trigger('change');
    $('#ddlType').prop('disabled', false);
    console.log("✅ initCreateTaskForm bitti.");
}
function initAllSelect2() {
    const select2 = $('#editUser .select2');

    select2.each(function () {
        var $this = $(this);

        // Eğer daha önce wrap edilmişse tekrar wrap etme
        if (!$this.parent().hasClass('position-relative')) {
            $this.wrap('<div class="position-relative"></div>');
        }

        // Eğer select2 zaten aktifse destroy
        if ($this.hasClass("select2-hidden-accessible")) {
            $this.select2("destroy");
        }

        $this.select2({
            placeholder: $this.data("placeholder") || "Select value",
            allowClear: true,
            dropdownParent: $this.parent(),
            width: "100%"
        });
    });
}

function toggleTaskMeetingFields(typeId) {

    console.log("toggleTaskMeetingFields çalışıyor → type:", typeId);

    // TASK = 1, MEETING = 2 varsayıyoruz
    const isTask = typeId == 1;
    const isMeeting = typeId == 2;

    toggleScheduleTab(typeId);
    // Bölümleri göster/gizle
    if (isTask) {
        $("#taskElements").removeClass("d-none");
        $("#meetingElements").addClass("d-none");

        // workflow yanındaki alan → ASSIGNEE
        $("#assignOrAttend").html(`
            <label class="form-label" for="ddlAssignee">Assignee</label>
            <select id="ddlAssignee" class="select2 form-select">
            <option value=""></option>
            </select>
        `);

        $('#ddlAssignee').select2({
            placeholder: "Select assignee",
            allowClear: true,
            dropdownParent: $('#editUser'),
            width: "100%"
        });

        loadCreateTaskAssignee(); // API'den doldurmak için varsa bu fonksiyon

    } else if (isMeeting) {
        $("#taskElements").addClass("d-none");
        $("#meetingElements").removeClass("d-none");

        // workflow yanındaki alan → ATTENDEES
        $("#assignOrAttend").html(`
            <label class="form-label" for="ddlAttendees">Attendees</label>
            <select id="ddlAttendees" class="select2 form-select" multiple>
            <option value=""></option>
            </select>
        `);
        $('#ddlAttendees').select2({
            placeholder: "Select attendees",
            allowClear: true,
            dropdownParent: $('#editUser'),
            width: "100%"
        });
        loadCreateTaskAttendees(); // API'den doldurmak için varsa

    } else {
        console.warn("Bilinmeyen typeId:", typeId);
    }

    // Yeni oluşturulan Select2 elementleri yeniden initialize et
    $('#assignOrAttend .select2').select2({
        dropdownParent: $('#editUser'),
        width: "100%"
    });

}
$('select.select2').each(function () {
    if ($(this).find("option[value='']").length === 0) {
        $(this).prepend(`<option value=""></option>`);
    }
});
let startPicker, endPicker;
let isEditMode = false;
function initTaskDatePickers() {

    const today = new Date().toISOString().split("T")[0];

    startPicker = flatpickr("#txt-start-date", {
        dateFormat: "Y-m-d",
        minDate: isEditMode ? null : today,   // EDIT mode → geçmişe izin
        static: true,
        allowInput: true,
        disableMobile: "true",
        onChange: function (selectedDates) {
            if (selectedDates.length > 0) {
                const start = selectedDates[0];
                endPicker.set("minDate", start);
            }
        }
    });

    endPicker = flatpickr("#txt-end-date", {
        dateFormat: "Y-m-d",
        minDate: isEditMode ? null : today,
        static: true,
        allowInput: true,
        disableMobile: "true",
        onChange: function (selectedDates) {
            if (selectedDates.length > 0) {
                const end = selectedDates[0];
                startPicker.set("maxDate", end);
            }
        }
    });
}

function toggleScheduleTab(typeId) {
    const isMeeting = typeId == 2;
    const $tab = $("#schedule-tab");

    if (isMeeting) {
        // Disable
        $tab.addClass("disabled").attr("tabindex", "-1").attr("aria-disabled", "true");

        // General tab'a geri döndür
        $("#general-tab").tab("show");

        console.log("📌 Schedule tab disabled (meeting seçildi)");
    } else {
        // Enable
        $tab.removeClass("disabled").removeAttr("tabindex").attr("aria-disabled", "false");

        console.log("📌 Schedule tab enabled (task seçildi)");
    }
}

$(document).on("click", "#external-tasks .fc-event", async function () {

    const taskId = $(this).data("taskId");
    if (!taskId) {
        console.error("Task ID bulunamadı");
        return;
    }

    console.log("Sidebar task clicked →", taskId);

    await openTaskEditModal(taskId);
});

async function openTaskEditModal(taskId) {
    isEditMode = true;
    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskDetailById/${taskId}`;

    try {
        const res = await fetch(url);
        const result = await res.json();

        if (!result?.data) {
            console.error("Task not found:", result);
            return;
        }

        const task = result.data;

        // FORM FILL
        populateEditTaskForm(task);

        //// UPDATE MODE ACTIVE
        prepareEditMode(task);

        // OPEN MODAL
        const modal = new bootstrap.Modal(document.getElementById('editUser'));
        modal.show();

    } catch (err) {
        console.error("openTaskEditModal error:", err);
    }
}

async function populateEditTaskForm(task) {

    await initCreateTaskForm();

    // Common
    $('#txt-name').val(task.name);
    $('#txt-description').val(task.description);
    $('#ddlType').val(task.typeId);
    $('#ddlType').prop('disabled', true);
    $('#ddlType').trigger('change');
    $('#ddlCategory').val(task.categId).trigger("change");
    $('#ddlWorkflow').val(task.workflowId);
    $('#ddlWorkflow').trigger('change');
    console.log("OPTIONS:", $("#ddlAssignee option").map((i, x) => x.value).get());
    console.log("TASK IDS:", task.assigneeIds);
    setTimeout(() => {
        $('#ddlAssignee').val(task.assigneeIds[0]).trigger('change');
    }, 50);
    $('#ddlStatus').val(task.statusId).trigger("change");
    $('#ddlPriority').val(task.priorityId).trigger("change");
    const startDate = task.startDate.split("T")[0];
    $("#txt-start-date").val(startDate);

    const endDate = task.endDate.split("T")[0];
    $("#txt-end-date").val(endDate);

    $('#txt-estimated-hour').val(task.estimatedHour);
    if (task.scheduleItems) {
        populateScheduleForEdit(task.scheduleItems);
    }

}
let isPopulatingSchedule = false;
async function populateScheduleForEdit(scheduleItemsResponse) {

    if (!Array.isArray(scheduleItemsResponse) || scheduleItemsResponse.length === 0)
        return;
    isPopulatingSchedule = true; 
    // Eski schedule satırlarını temizle
    $("#scheduleList").empty();
    scheduleItems = [];
    scheduleCounter = 0;

    for (const s of scheduleItemsResponse) {

        // Yeni schedule satırı oluştur
        addScheduleItem();

        const id = scheduleCounter;

        // Elemanları seç
        const startDateEl = document.getElementById(`start-date-${id}`);
        const endDateEl = document.getElementById(`end-date-${id}`);
        const startTimeEl = document.getElementById(`start-time-${id}`);
        const endTimeEl = document.getElementById(`end-time-${id}`);
        const allDayEl = document.getElementById(`start-date-${id}`).closest(".schedule-item").querySelector(".all-day-checkbox");

        // All Day set edilecek
        allDayEl.checked = s.isAllDay === true;

        // Tarihleri doldur
        if (s.startDate) scheduleItems.find(x => x.id === id).startPicker.setDate(s.startDate, true);
        if (s.endDate) scheduleItems.find(x => x.id === id).endPicker.setDate(s.endDate, true);

        // Saat alanları doldur
        if (!s.isAllDay) {
            if (s.startTime) $(startTimeEl).val(s.startTime).trigger("change.select2");
            if (s.endTime) $(endTimeEl).val(s.endTime).trigger("change.select2");
        } else {
            // All-day ise saatler gizlenmeli
            $(startTimeEl).val(null).trigger("change.select2");
            $(endTimeEl).val(null).trigger("change.select2");
        }

        // All day UI toggle
        toggleAllDay(startDateEl.closest(".schedule-item"), s.isAllDay);

        // Validasyon tetikle
        validateSchedule(id);
    }

    isPopulatingSchedule = false;
}

function prepareEditMode(task) {
    $("#editUser .modal-title").text("Edit Task / Meeting");
    $(".createTask")
        .attr("data-mode", "edit")
        .attr("data-id", task.id)
        .text("Update");
}
//------------ Schedule tab için date picker  ------------//
let scheduleCounter = 0;
let scheduleItems = [];
function initScheduleSystem() {
    
    // Modal açıldığında ilk satır otomatik gelsin
    addScheduleItem();
}
document.getElementById("btnAddScheduleItem")
    .addEventListener("click", addScheduleItem);


function addScheduleItem() {
    scheduleCounter++;

    const template = document.getElementById("schedule-item-template");
    const container = document.getElementById("scheduleList");

    const newItem = template.content.cloneNode(true);
    const row = newItem.querySelector(".schedule-item");

    // inputları seç
    const startDate = row.querySelector(".start-date");
    const endDate = row.querySelector(".end-date");
    const startTime = row.querySelector(".start-time");
    const endTime = row.querySelector(".end-time");
    const allDay = row.querySelector(".all-day-checkbox");

    // unique ID ver
    startDate.id = `start-date-${scheduleCounter}`;
    endDate.id = `end-date-${scheduleCounter}`;
    startTime.id = `start-time-${scheduleCounter}`;
    endTime.id = `end-time-${scheduleCounter}`;

    container.appendChild(newItem);

    initScheduleItem(scheduleCounter);
}

function initScheduleItem(id) {

    const startDateInput = document.getElementById(`start-date-${id}`);
    const endDateInput = document.getElementById(`end-date-${id}`);
    const startTimeInput = document.getElementById(`start-time-${id}`);
    const endTimeInput = document.getElementById(`end-time-${id}`);
    const row = startDateInput.closest(".schedule-item");

    // TIME DROPDOWNLARI 30 DK ARALIKLARLA
    fillTimeDropdown(startTimeInput);
    fillTimeDropdown(endTimeInput);

    // SELECT2
    $(startTimeInput).select2({
        dropdownParent: $('#editUser'),   // 🔥 Modal referansı
        width: '100%'
    });
    $(endTimeInput).select2({
        dropdownParent: $('#editUser'),   // 🔥 Modal referansı
        width: '100%'
    });

    const today = new Date().toISOString().split("T")[0];

    const startPicker = flatpickr(startDateInput, {
        dateFormat: "Y-m-d",
        minDate: today,
        disableMobile: true,
        allowInput: true,
        static: true,
        appendTo: document.body,
        position: "above",
        onChange: function () { validateSchedule(id); }
    });

    const endPicker = flatpickr(endDateInput, {
        dateFormat: "Y-m-d",
        minDate: today,
        disableMobile: true,
        allowInput: true,
        static: true,
        appendTo: document.body,
        onChange: function () { validateSchedule(id); }
    });

    scheduleItems.push({ id, startPicker, endPicker });

    // REMOVE BUTTON
    row.querySelector(".btnRemoveScheduleItem")
        .addEventListener("click", () => removeScheduleItem(id, row));

    // ALL DAY CHECKBOX
    row.querySelector(".all-day-checkbox")
        .addEventListener("change", (e) => toggleAllDay(row, e.target.checked));
}

function removeScheduleItem(id, row) {
    row.remove();
    scheduleItems = scheduleItems.filter(x => x.id !== id);
}
function toggleAllDay(row, isAllDay) {
    const timeSections = row.querySelectorAll(".time-section");

    if (isAllDay) {
        timeSections.forEach(t => t.classList.add("d-none"));
    } else {
        timeSections.forEach(t => t.classList.remove("d-none"));
    }
}
function fillTimeDropdown(el) {
    el.innerHTML = "";
    for (let h = 0; h < 24; h++) {
        for (let m of [0, 30]) {
            const time = `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
            el.innerHTML += `<option value="${time}">${time}</option>`;
        }
    }
}

function validateSchedule(id) {
    if (isPopulatingSchedule) return;
    const item = scheduleItems.find(x => x.id === id);
    if (!item) return;

    const startDate = item.startPicker.selectedDates[0];
    const endDate = item.endPicker.selectedDates[0];
    if (!startDate || !endDate) return;

    const row = document.getElementById(`start-date-${id}`).closest(".schedule-item");
    const isAllDay = row.querySelector(".all-day-checkbox")?.checked;

    const startTimeVal = document.getElementById(`start-time-${id}`)?.value || null;
    const endTimeVal = document.getElementById(`end-time-${id}`)?.value || null;

    // All Day ise 00:00–23:59, değilse seçilen saatleri kullan
    const currentStart = isAllDay
        ? buildDateTime(startDate, null, "start")
        : buildDateTime(startDate, startTimeVal);

    const currentEnd = isAllDay
        ? buildDateTime(endDate, null, "end")
        : buildDateTime(endDate, endTimeVal);

    if (!currentStart || !currentEnd) {
        // Saatler henüz seçilmediyse erken validate etme
        return;
    }

    // 1) Kendi içinde: End < Start olamaz
    if (currentEnd <= currentStart) {
        alert("End date & time must be later than start date & time.");

        // Son seçileni temizlemek için end'i sıfırlıyoruz
        item.endPicker.clear();
        const endTimeEl = document.getElementById(`end-time-${id}`);
        if (endTimeEl) {
            $(endTimeEl).val(null).trigger("change");
        }
        return;
    }

    // 2) Diğer satırlarla overlap kontrolü
    const currentStartIso = currentStart.toISOString();
    const currentEndIso = currentEnd.toISOString();

    for (let x of scheduleItems) {
        if (x.id === id) continue;

        const otherStartDate = x.startPicker.selectedDates[0];
        const otherEndDate = x.endPicker.selectedDates[0];
        if (!otherStartDate || !otherEndDate) continue;

        const otherRow = document.getElementById(`start-date-${x.id}`).closest(".schedule-item");
        const otherAllDay = otherRow.querySelector(".all-day-checkbox")?.checked;

        const otherStartTimeVal = document.getElementById(`start-time-${x.id}`)?.value || null;
        const otherEndTimeVal = document.getElementById(`end-time-${x.id}`)?.value || null;

        const otherStart = otherAllDay
            ? buildDateTime(otherStartDate, null, "start")
            : buildDateTime(otherStartDate, otherStartTimeVal);

        const otherEnd = otherAllDay
            ? buildDateTime(otherEndDate, null, "end")
            : buildDateTime(otherEndDate, otherEndTimeVal);

        if (!otherStart || !otherEnd) continue;

        // Tam aynı ise zaten çakışma
        if (otherStart.toISOString() === currentStartIso &&
            otherEnd.toISOString() === currentEndIso) {
            alert("This schedule already exists (same start and end).");
            clearCurrentScheduleRow(id, item);
            return;
        }

        // Overlap kontrolü:
        // currentStart < otherEnd  &&  currentEnd > otherStart
        if (currentStart < otherEnd && currentEnd > otherStart) {
            alert("This schedule overlaps with another time slot.");
            clearCurrentScheduleRow(id, item);
            return;
        }
    }
}
function clearCurrentScheduleRow(id, item) {
    item.startPicker.clear();
    item.endPicker.clear();

    const startTimeEl = document.getElementById(`start-time-${id}`);
    const endTimeEl = document.getElementById(`end-time-${id}`);

    if (startTimeEl) $(startTimeEl).val(null).trigger("change");
    if (endTimeEl) $(endTimeEl).val(null).trigger("change");
}

function collectScheduleItems() {
    const items = [];

    $("#scheduleList .schedule-item").each(function () {

        const startDate = $(this).find(".start-date").val();
        const endDate = $(this).find(".end-date").val();
        const startTime = $(this).find(".start-time").val();
        const endTime = $(this).find(".end-time").val();
        const isAllDay = $(this).find(".all-day-checkbox").is(":checked");

        // All day işaretliyse time alanları yok sayılır
        let start = null;
        let end = null;

        if (startDate) {
            if (isAllDay) {
                start = `${startDate}T00:00:00`;
            } else if (startTime) {
                start = `${startDate}T${startTime}:00`;
            }
        }

        if (endDate) {
            if (isAllDay) {
                end = `${endDate}T23:59:59`;
            } else if (endTime) {
                end = `${endDate}T${endTime}:00`;
            }
        }

        items.push({
            startDate,
            endDate,
            startTime,
            endTime,
            start,
            end,
            isAllDay
        });
    });

    return items;
}

function buildDateTime(dateObj, timeStr, allDayStartOrEnd) {
    if (!dateObj) return null;

    const dt = new Date(dateObj);

    // All day ise sabah 00:00 veya gece 23:59 olarak kabul et
    if (allDayStartOrEnd === "start") {
        dt.setHours(0, 0, 0, 0);
        return dt;
    }

    if (allDayStartOrEnd === "end") {
        dt.setHours(23, 59, 59, 999);
        return dt;
    }

    if (!timeStr) return null;

    const [h, m] = timeStr.split(":").map(Number);
    dt.setHours(h || 0, m || 0, 0, 0);

    return dt;
}



//------------------- Meeting Fields -------------------//
let meetingStartPicker, meetingEndPicker;

function initMeetingPickers() {
    const $modal = $('#editUser');
    const today = new Date().toISOString().split("T")[0];

    meetingStartPicker = flatpickr("#txt-meeting-start-date", {
        dateFormat: "Y-m-d",
        minDate: today,
        disableMobile: true,
        appendTo: $modal[0],    // 🔥 popup modal içinde
        position: "above",      // 🔥 yukarı açılır
        onChange: function (selectedDates) {
            const start = selectedDates[0];
            const end = meetingEndPicker.selectedDates[0];

            // End start’tan küçük olamaz
            meetingEndPicker.set("minDate", start);

            if (end && end < start) {
                meetingEndPicker.clear();
            }

            validateMeetingTimes();
        }
    });

    meetingEndPicker = flatpickr("#txt-meeting-end-date", {
        dateFormat: "Y-m-d",
        minDate: today,
        disableMobile: true,
        appendTo: $modal[0],
        position: "above",
        onChange: function (selectedDates) {
            const end = selectedDates[0];
            const start = meetingStartPicker.selectedDates[0];

            meetingStartPicker.set("maxDate", end);

            if (start && start > end) {
                meetingStartPicker.clear();
            }

            validateMeetingTimes();
        }
    });
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
function fillMeetingTimes() {
    const ddlStart = $("#ddlStartTime");
    const ddlEnd = $("#ddlEndTime");

    ddlStart.empty();
    ddlEnd.empty();

    const times = generateTimeOptions("00:00", "23:45", 15);

    times.forEach(t => {
        ddlStart.append(`<option value="${t.value}">${t.text}</option>`);
        ddlEnd.append(`<option value="${t.value}">${t.text}</option>`);
    });

    ddlStart.select2({
        dropdownParent: $("#editUser"),
        placeholder: "Start time",
        width: "100%"
    });

    ddlEnd.select2({
        dropdownParent: $("#editUser"),
        placeholder: "End time",
        width: "100%"
    });
}

function validateMeetingTimes() {

    if (!meetingStartPicker || !meetingEndPicker) {
        console.warn("⛔ Meeting pickers henüz initialize edilmedi!");
        return;
    }

    const startDate = meetingStartPicker.selectedDates[0];
    const endDate = meetingEndPicker.selectedDates[0];
    const startTime = $("#ddlStartTime").val();
    const endTime = $("#ddlEndTime").val();

    if (!startDate || !endDate || !startTime || !endTime) return;

    const startDT = new Date(startDate);
    const [sh, sm] = startTime.split(":").map(Number);
    startDT.setHours(sh, sm, 0, 0);

    const endDT = new Date(endDate);
    const [eh, em] = endTime.split(":").map(Number);
    endDT.setHours(eh, em, 0, 0);

    if (endDT <= startDT) {

        showToast("End time must be later than start time", "error");


        meetingEndPicker.clear();
        $("#ddlEndTime").val(null).trigger("change");
    }
}
$("#ddlStartTime").on("change", validateMeetingTimes);
$("#ddlEndTime").on("change", validateMeetingTimes);
//---------------------------- end Meeting Fields ----------------------------//

$('#editUser').on('hidden.bs.modal', function () {
    resetCreateTaskMeetingModal();
});
function resetCreateTaskMeetingModal() {

    console.log("🔄 Modal Reset: editUser temizleniyor...");

    // 1) Tüm inputları temizle
    $('#editUser input[type="text"]').val('');
    $('#editUser textarea').val('');
    $('#editUser input[type="number"]').val('');

    // 2) Select2 dropdownları temizle
    $('#editUser select.select2').val(null).trigger('change');

    // 3) Flatpickr temizleme
    if (meetingStartPicker) meetingStartPicker.clear();
    if (meetingEndPicker) meetingEndPicker.clear();

    // 4) Start/End time dropdownlarını sıfırlama
    $('#ddlStartTime').val(null).trigger('change');
    $('#ddlEndTime').val(null).trigger('change');

    // 5) Tab’ları başa almak
    $('#general-tab').tab('show');
    toggleScheduleTab(1); // task default → schedule tab enable

    // 6) Task vs Meeting alanlarını başa döndürmek
    $('#taskElements').addClass('d-none');
    $('#meetingElements').addClass('d-none');
    $("#assignOrAttend").empty();

    // 7) Schedule item list temizleme
    scheduleItems = [];
    $('#scheduleList').empty();

    // 8) Type dropdownunu sıfırlama
    $('#ddlType').val(null).trigger('change');

    console.log("✨ Modal tamamen sıfırlandı.");
}
async function createTask() {

    console.log("⏳ createTask() çalıştı...");

    // -------------------------
    // 1) TYPE (Task = 1, Meeting = 2)
    // -------------------------
    const typeId = Number($("#ddlType").val());
    if (!typeId) {
        showToast("Please select a type", "error");
        return;
    }

    // -------------------------
    // 2) COMMON FIELDS (task & meeting ortak)
    // -------------------------
    const payload = {
        name: $("#txt-name").val()?.trim(),
        description: $("#txt-description").val()?.trim(),
        typeId: typeId,
        workflowId: $("#ddlWorkflow").val(),
        categoryId: $("#ddlCategory").val(),
        ownerId: window.getUserId() // global function from _Layout
    };

    if (!payload.name) {
        showToast("Please enter a name", "error");
        return;
    }

    // ---------------------------------------------------
    // 3) TASK MODE PAYLOAD (typeId = 1)
    // ---------------------------------------------------
    if (typeId === 1) {

        payload.assigneeIds = [$("#ddlAssignee").val()] || [];
        payload.statusId = Number($("#ddlStatus").val());
        payload.priorityId = Number($("#ddlPriority").val());

        payload.startDate = $("#txt-start-date").val();
        payload.endDate = $("#txt-end-date").val();
        payload.estimatedHour = Number($("#txt-estimated-hour").val()) || 0;

        if (!payload.assigneeIds.length) {
            showToast("Please select assignee", "error");
            return;
        }
        if (!payload.startDate || !payload.endDate) {
            showToast("Please fill start & end dates", "error");
            return;
        }
    }

    // ---------------------------------------------------
    // 4) MEETING MODE PAYLOAD (typeId = 2)
    // ---------------------------------------------------
    if (typeId === 2) {

        payload.assigneeIds = $("#ddlAttendees").val() || [];

        const startDate = $("#txt-meeting-start-date").val();
        const endDate = $("#txt-meeting-end-date").val();
        const startTime = $("#ddlStartTime").val();
        const endTime = $("#ddlEndTime").val();

        if (!payload.assigneeIds.length) {
            showToast("Please select attendees", "error");
            return;
        }

        if (!startDate || !endDate || !startTime || !endTime) {
            showToast("Please fill meeting date & time", "error");
            return;
        }

        payload.startDate = `${startDate}T${startTime}:00`;
        payload.endDate = `${endDate}T${endTime}:00`;
    }

    // ---------------------------------------------------
    // 5) SCHEDULE ITEMS (eğer task ise – meetingde yok)
    // ---------------------------------------------------
    if (typeId === 1) {
        payload.scheduleItems = collectScheduleItems();
    }
    payload.createdBy = window.getUserName(); // global function from _Layout
    // ---------------------------------------------------
    // 6) FINAL LOG
    // ---------------------------------------------------
    console.log("📦 Final Payload: ", payload);

    // ---------------------------------------------------
    // 7) API CALL
    // ---------------------------------------------------
    try {
        const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/CreateTaskOrMeeting`;

        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await res.json();

        if(!result.errors || result.errors.length === 0) {
            showToast("Task / Meeting created successfully", "success");
            $("#editUser").modal("hide");

            if (typeof calendar !== "undefined") {
                console.log("🔄 Calendar refresh trigger!");
                calendar.refetchEvents();
                await loadCalendarSidebar();
            }
        } else {
            showToast(result.message || "Error", "error");
        }

    } catch (err) {
        console.error("❌ CreateTask error:", err);
        showToast("Unexpected error", "error");
    }
}
async function updateTask(taskId) {

    console.log("♻️ updateTask() çalıştı... ID:", taskId);

    // -------------------------
    // 1) TYPE (Task / Meeting)
    // -------------------------
    const typeId = Number($("#ddlType").val());
    if (!typeId) {
        showToast("Please select a type", "error");
        return;
    }

    // -------------------------
    // 2) COMMON FIELDS
    // -------------------------
    const payload = {
        id: taskId,
        name: $("#txt-name").val()?.trim(),
        description: $("#txt-description").val()?.trim(),
        typeId,
        workflowId: $("#ddlWorkflow").val(),
        categoryId: $("#ddlCategory").val(),
        ownerId: window.getUserId(),
        modifiedBy: window.getUserName()
    };

    if (!payload.name) {
        showToast("Name is required", "error");
        return;
    }

    // ---------------------------------------------------
    // 3) TASK MODE PAYLOAD (typeId = 1)
    // ---------------------------------------------------
    if (typeId === 1) {

        payload.assigneeIds = [$("#ddlAssignee").val()] || [];
        payload.statusId = Number($("#ddlStatus").val());
        payload.priorityId = Number($("#ddlPriority").val());

        payload.startDate = $("#txt-start-date").val();
        payload.endDate = $("#txt-end-date").val();
        payload.estimatedHour = Number($("#txt-estimated-hour").val()) || 0;

        if (!payload.assigneeIds.length) {
            showToast("Please select assignee", "error");
            return;
        }
        if (!payload.startDate || !payload.endDate) {
            showToast("Please fill start & end dates", "error");
            return;
        }
    }

    // ---------------------------------------------------
    // 4) MEETING MODE PAYLOAD (typeId = 2)
    // ---------------------------------------------------
    if (typeId === 2) {

        payload.assigneeIds = $("#ddlAttendees").val() || [];

        const startDate = $("#txt-meeting-start-date").val();
        const endDate = $("#txt-meeting-end-date").val();
        const startTime = $("#ddlStartTime").val();
        const endTime = $("#ddlEndTime").val();

        if (!payload.assigneeIds.length) {
            showToast("Please select attendees", "error");
            return;
        }

        if (!startDate || !endDate || !startTime || !endTime) {
            showToast("Please fill meeting date & time", "error");
            return;
        }

        payload.startDate = `${startDate}T${startTime}:00`;
        payload.endDate = `${endDate}T${endTime}:00`;
    }

    // ---------------------------------------------------
    // 5) SCHEDULE ITEMS (Task modunda)
    // ---------------------------------------------------
    if (typeId === 1) {
        payload.scheduleItems = collectScheduleItems();
    }

    console.log("📦 UPDATE PAYLOAD:", payload);

    // ---------------------------------------------------
    // 6) API CALL
    // ---------------------------------------------------
    try {
        const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/UpdateTaskOrMeeting`;

        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await res.json();

        if (!result.errors || result.errors.length === 0) {

            showToast("Task / Meeting updated successfully", "success");
            $("#editUser").modal("hide");

            if (typeof calendar !== "undefined") {
                calendar.refetchEvents();
                await loadCalendarSidebar();
            }

        } else {
            showToast(result.message || "Error updating task", "error");
        }

    } catch (err) {
        console.error("❌ updateTask error:", err);
        showToast("Unexpected error", "error");
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
// SAVE butonuna basılınca createTask fonksiyonunu çağır
$(document).on("click", ".createTask",async function () {
    const mode = $(this).attr("data-mode") || "create";
    const id = $(this).attr("data-id") || null;

    if (mode === "edit" && id) {
        await updateTask(id);
    } else {
        await createTask();
    }
});
//----------------------------------- END ADD EVENT MODAL -----------------------------------//



let calendar;
//---------------------------- FullCalendar Initialization ----------------------------//
async function fetchEvents(info, successCallback, failureCallback) {
    const userId = getUserId();
    if (!userId) {
        console.warn("UserId veya token bulunamadı.");
        failureCallback && failureCallback("UserId/token eksik");
        return;
    }

    try {
        const apiUrl = `${window.ApiBaseUrl}/services/DitenPPM/Task/GetCalendarEvents/${userId}`;
        const response = await fetch(apiUrl);

        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const result = await response.json();

        const events = (result.data || []).map(ev => ({
            id: ev.id,
            title: ev.title,
            start: ev.startDate,
            end: ev.endDate,
            allDay: ev.allDay,
            extendedProps: {
                calendar: ev.calendarTypeName || "Low",
                description: ev.description || "",
                type: ev.calendarTypeName,
                priority: ev.calendarPriorityName,
                status: ev.calendarStatusName,
                fromUserName: ev.fromUserName,
                toUserName: ev.toUserName,
                fromUserId: ev.fromUserId,
                toUserId: ev.toUserId,
                taskEndDate: ev.taskEndDate,
            }
        }));

        successCallback(events);
    } catch (error) {
        console.error("Takvim eventleri alınamadı:", error);
        failureCallback && failureCallback(error);
    }
}
//-------------------------------- END FullCalendar Initialization --------------------------------//

//---------------------------------- Task Detail Modal ----------------------------------//
let timerInterval = null;
let secondsCounter = 0;
let timerStartDate = null;   // START'a bastığımız an
const card = document.getElementById("tdCard");
const title = document.getElementById("tdTsTitle");
const liveTimer = document.getElementById("tdLiveTimer");
const btn = document.getElementById("tdTimesheetBtn");
const logged = document.getElementById("tdLogged");
let activeTrackerTaskId = null;
let floatingInterval = null;
let floatingStartDate = null;

// TIMER FORMATTER
function formatMMSS(sec) {
    const m = Math.floor(sec / 60).toString().padStart(2, "0");
    const s = (sec % 60).toString().padStart(2, "0");
    return `${m}:${s}`;
}
// START
 function startTimesheet() {
    timerStartDate = new Date();
    card.classList.remove("bg-label-primary");
    card.classList.add("bg-label-warning");

    title.classList.remove("text-primary");
    title.classList.add("text-warning");

    btn.classList.remove("btn-primary");
    btn.classList.add("btn-warning");

    btn.innerHTML = `<i class="icon-base bx bx-pause"></i> Stop Timesheet`;

    liveTimer.classList.remove("d-none");

    secondsCounter = 0;
    liveTimer.textContent = "00:00";
    enablePiPClickStop();
    openPiP();
    drawTimer("00:00");

    timerInterval = setInterval(() => {
        secondsCounter++;
        const mm = String(Math.floor(secondsCounter / 60)).padStart(2, "0");
        const ss = String(secondsCounter % 60).padStart(2, "0");
        const t = mm + ":" + ss;
        liveTimer.textContent = formatMMSS(secondsCounter);
        drawTimer(t);
    }, 1000);
    //enablePiPClickStop();
    //showFloatingTracker(window.currentTaskId, timerStartDate);
    //openTimerPopup(currentTaskId, timerStartDate);
}
let canvas = document.createElement("canvas");
canvas.width = 200;
canvas.height = 100;
// görünür boyut
canvas.style.width = "100px";
canvas.style.height = "50px";
let ctx = canvas.getContext("2d");
ctx.scale(2, 2);
let stream = canvas.captureStream(30);
let pipVideo = document.getElementById("pipVideo");
pipVideo.srcObject = stream;



async function openPiP() {
   /* try {*/
        await pipVideo.play(); // PiP için şart
        //pipVideo.dispatchEvent(new Event("loadeddata"));
        //pipVideo.dispatchEvent(new Event("canplay"));
        //pipVideo.dispatchEvent(new Event("canplaythrough"));
        if (document.pictureInPictureElement) {
            await document.exitPictureInPicture();
        }
        await pipVideo.requestPictureInPicture();
    //} catch (err) {
    //    console.error("PiP error:", err);
    //}
}
function drawTimer(timeText) {
    ctx.clearRect(0, 0, 200, 100);

    // Background
    ctx.fillStyle = "#1f1f1f";
    ctx.fillRect(0, 0, 200, 100);

    // === TITLE ===
    let title = window.currentTaskName || "Time Tracking";

    let fontSize = 8;
    ctx.font = fontSize + "px Arial";

    while (ctx.measureText(title).width > 100 - 6 && fontSize > 5) {
        fontSize--;
        ctx.font = fontSize + "px Arial";
    }

    ctx.fillStyle = "#fff";
    ctx.fillText(title, 3, 10);

    // === TIMER ===
    ctx.fillStyle = "#ffb74d";
    ctx.font = "18px Arial";

    const textWidth = ctx.measureText(timeText).width;
    const centerX = (100 - textWidth) / 2;

    ctx.fillText(timeText, centerX, 38);
}
function enablePiPClickStop() {
    window.addEventListener("focus", onPiPFocus);
}

function onPiPFocus() {
    // PiP’e tıklanmış demektir → stop yap
    stopTimesheet();

    // 2) Task Detail Modal kapalıysa aç
    const modalEl = document.getElementById("taskDetailModal");
    const isModalOpen = modalEl.classList.contains("show");

    if (!isModalOpen && window.currentTaskId) {
        openTaskDetailModal({ id: window.currentTaskId, extendedProps: { taskId: window.currentTaskId } });
    }

    // 3) Event listener bir kere çalışsın
    window.removeEventListener("focus", onPiPFocus);
}





 //STOP

 //CLICK EVENT
btn.addEventListener("click", async () => {
    if (timerInterval === null) {
        startTimesheet();
    } else {
         stopTimesheet();
        timerInterval = null;
    }
});

async function openTaskDetailModal(event) {

    const taskId = event.extendedProps.taskId || event.id;
    window.currentTaskId = taskId;

    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskDetail/${taskId}`;

    const res = await fetch(url);
    const result = await res.json();

    if (result.errors != null && result.errors.length>0) {
        showToast("Task detail could not be loaded", "error");
        return;
    }

    const data = result.data;

    // FIELDS FILL
    document.getElementById("tdTitle").innerText = data.name;
    window.currentTaskName = data.name;
    document.getElementById("tdPriority").innerText = data.priorityName;
    document.getElementById("tdPriority").className =
        `badge fs-6 ${getPriorityBadgeColor(data.priorityId)}`;

    document.getElementById("tdDescription").innerText =
        data.description || "-";

    document.getElementById("tdAssignee").innerText =
        data.assignees?.map(a => a.name).join(", ") || "-";

    document.getElementById("tdEstimated").innerText =
        data.estimatedHour ? `${data.estimatedHour} minute` : "-";

    document.getElementById("tdDeadline").innerText =
        data.deadlinePretty ? data.deadlinePretty : "-";

    document.getElementById("tdLogged").innerText =
        data.totalLogged || "0m";

    renderTimesheetList(data.timerSessions);

    // Modal aç
    new bootstrap.Modal(document.getElementById("taskDetailModal")).show();
}
function getPriorityBadgeColor(id) {
    switch (id) {
        case 4: return "bg-danger";
        case 3: return "bg-warning";
        case 2: return "bg-info";
        case 1: return "bg-primary";
        default: return "bg-secondary";
    }
}
function renderTimesheetList(list) {
    const container = document.getElementById("tdTimesheetList");
    container.innerHTML = "";

    if (!list || list.length === 0) {
        container.innerHTML = `
        <div class="alert alert-secondary border shadow-sm p-3" role="alert">
            <h6 class="alert-heading d-flex align-items-center gap-2 mb-2">
                <span class="alert-icon rounded-circle bg-label-secondary p-1 d-flex align-items-center justify-content-center" 
                      style="width:28px;height:28px;">
                    <i class="icon-base bx bx-time-five fs-5"></i>
                </span>
                No Timesheet Records
            </h6>
            <p class="mb-0 text-muted small">
                You haven't logged any time for this task yet.  
                Start tracking using the <strong>Start Timesheet</strong> button above.
            </p>
        </div>
    `;
        return;
    }


    list.forEach((s, i) => {
        container.innerHTML += `
            <div class="d-flex justify-content-between align-items-center p-2 mb-1 bg-body border rounded">
                <div>
                    <span class="badge bg-primary me-2">${i + 1}</span>
                    ${s.endPretty}
                </div>
                <div class="text-muted text-primary">${s.duration}m</div>
            </div>
        `;
    });
}

async function stopTimesheet() {

    const now = new Date();            // STOP zamanı
    clearInterval(timerInterval);
    timerInterval = null;

    // ⏳ DOĞRU SÜRE → start = timerStartDate, end = now
    const payload = {
        slotId: window.currentTaskId,
        start: timerStartDate.toISOString(),   // START’a basılan an
        end: now.toISOString(),                // STOP’a basılan an
        userId: window.getUserId()
    };

    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/CreateTimesheetEntry`;

    const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
    });

    const result = await res.json();

    // ❌ Hatalı logic
    // if (result.errors == null && result.errors.length > 0)

    // ✔ Doğru error kontrolü
    if (result.errors != null && result.errors.length>0) {
        showToast(result.errors?.join("<br>") || "Timesheet could not be saved", "error");
        return;
    }
    // UI eski haline dön
    resetTimesheetUI();
    if (document.pictureInPictureElement) {
        await document.exitPictureInPicture();
    }
    // Modal içeriğini yenile
    await reloadTaskDetail(window.currentTaskId);

    showToast("Timesheet saved", "success");


}
function resetTimesheetUI() {

    document.getElementById("tdLiveTimer").classList.add("d-none");

    document.getElementById("tdCard").classList.remove("bg-label-warning");
    document.getElementById("tdCard").classList.add("bg-label-primary");

    document.getElementById("tdTsTitle").classList.remove("text-warning");
    document.getElementById("tdTsTitle").classList.add("text-primary");

    const btn = document.getElementById("tdTimesheetBtn");
    btn.classList.remove("btn-warning");
    btn.classList.add("btn-primary");
    btn.innerHTML = `<i class="icon-base bx bx-play"></i> Start Timesheet`;
}
async function reloadTaskDetail(taskId) {
    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskDetail/${taskId}`;
    const res = await fetch(url);
    const result = await res.json();

    if (result.errors != null && result.errors.length>0) return;

    const d = result.data;

    // total
    document.getElementById("tdLogged").innerText = d.totalLogged;

    // logs list
    renderTimesheetList(d.timerSessions);
}

document.getElementById("btnCloseTask").addEventListener("click", openCloseTaskModal);

async function openCloseTaskModal() {
    const slotId = window.currentTaskId;

    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/GetTaskDetail/${slotId}`;

    const res = await fetch(url);
    const result = await res.json();

    if (result.errors != null && result.errors.length>0) {
        showToast("Task close detail could not be loaded", "error");
        return;
    }
    const data = result.data;

    //if (data.timerSessions == null || data.timerSessions.length<=0) {
    //    showToast("You must add a Time Tracker entry before closing this task.","warning");
    //    return;
    //}



    achievements = [];
    renderAchievements();
    challenges = [];
    renderChallenges();
    learnings = [];
    renderLearnings();
    nextSteps = [];
    renderNextSteps();
    // efficiency hesabı
    const eff = calculateEfficiency(data.estimatedHour, data.totalLogged);

    // Fill fields
    document.getElementById("ctTaskName").innerText = data.name;
    document.getElementById("ctTimeSpent").innerText = data.totalLogged;
    document.getElementById("ctEstimated").innerText = data.estimatedHour;
    document.getElementById("ctSessions").innerText = data.timerSessions.length +1;
    document.getElementById("ctEfficiency").innerText = eff.percent;
    document.getElementById("ctEfficiencyMultiplier").innerText = eff.multiplier;
    // renkli badge
    document.getElementById("ctEfficiency").className =
        `badge fs-6 ${getEfficiencyColor(eff.score)}`;

    // progress bar
    updateEfficiencyBar(eff.score);

    renderCloseTaskSessions(data.timerSessions);

    new bootstrap.Modal(document.getElementById("closeTaskModal")).show();
}
function calculateEfficiency(estimatedHour, totalLoggedPretty) {
    // totalLoggedPretty → "3m", "1h 20m 10s" gibi değerler geliyor
    const totalMinutes = parseDurationToMinutes(totalLoggedPretty);
    const estimatedMinutes = estimatedHour * 60;

    if (estimatedMinutes <= 0 || totalMinutes <= 0) {
        return {
            percent: "0%",
            multiplier: "0x",
            score: 0
        };
    }

    const efficiency = (estimatedMinutes / totalMinutes) * 100;

    return {
        percent: `${Math.round(efficiency)}%`,
        multiplier: `${Math.round(estimatedMinutes / totalMinutes)}x`,
        score: efficiency
    };
}
function parseDurationToMinutes(str) {
    let h = 0, m = 0, s = 0;

    if (!str) return 0;

    const hMatch = str.match(/(\d+)h/);
    const mMatch = str.match(/(\d+)m/);
    const sMatch = str.match(/(\d+)s/);

    if (hMatch) h = parseInt(hMatch[1]);
    if (mMatch) m = parseInt(mMatch[1]);
    if (sMatch) s = parseInt(sMatch[1]);

    return (h * 60) + m + (s / 60);
}
function getEfficiencyColor(score) {
    if (score < 100) return "bg-label-danger";   // kırmızı
    if (score === 100) return "bg-label-warning"; // sarı
    return "bg-label-success";                  // yeşil
}
function updateEfficiencyBar(score) {
    const bar = document.getElementById("ctEfficiencyBar");
    const capped = Math.min(score, 300); // çok uçuk skorlar barı patlatmasın

    bar.style.width = `${capped}%`;
    bar.className = `progress-bar ${getEfficiencyColor(score)}`;
}


function renderCloseTaskSessions(list) {
    const container = document.getElementById("ctTimerList");
    container.innerHTML = "";

    if (!list || list.length === 0) {
        container.innerHTML = `<div class="text-muted small">No sessions yet</div>`;
        return;
    }

    list.forEach((s, i) => {
        container.innerHTML += `
            <div class="d-flex justify-content-between align-items-center p-2 rounded bg-body border mb-2">
                <div>
                    <span class="badge bg-primary me-2">${i + 1}</span>
                    ${s.endPretty}
                </div>
                <div class="text-primary">${s.duration}</div>
            </div>
        `;
    });
}
//-------------- Achievements--------------------------//
function enableEnterToAdd(inputSelector, addCallback) {
    $(inputSelector).on('keypress', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            const value = $(this).val().trim();

            if (!value) return;

            addCallback(value);
            $(this).val('');
        }
    });
}

// Achievements listesi
let achievements = [];

document.getElementById("btnAddAchievement").addEventListener("click", () => {
    const input = document.getElementById("ctAchievementInput");
    const value = input.value.trim();

    if (!value) return;

    achievements.push(value);
    renderAchievements();
    input.value = "";
});
enableEnterToAdd("#ctAchievementInput", function (value) {
    achievements.push(value);
    renderAchievements();
});
// Listenin render edilmesi
function renderAchievements() {
    const container = document.getElementById("ctAchievementsList");
    container.innerHTML = "";

    achievements.forEach((ach, index) => {
        const item = document.createElement("span");
        item.className = "badge bg-label-dark me-2 mb-2 p-2";
        item.style.cursor = "default";

        item.innerHTML = `
            ${ach}
            <i class="bx bx-x ms-1" 
               style="cursor:pointer;" 
               onclick="removeAchievement(${index})"></i>
        `;

        container.appendChild(item);
    });
}

// Silme fonksiyonu
function removeAchievement(index) {
    achievements.splice(index, 1);
    renderAchievements();
}
//---------------------------------- Challenges Faced--------------------------//
// Challenge listesi
let challenges = [];

document.getElementById("btnAddChallenge").addEventListener("click", () => {
    const input = document.getElementById("ctChallengeInput");
    const value = input.value.trim();

    if (!value) return;

    challenges.push(value);
    renderChallenges();
    input.value = "";
});

// Challenge listeyi render eden fonksiyon
function renderChallenges() {
    const container = document.getElementById("ctChallengesList");
    container.innerHTML = "";

    challenges.forEach((ch, index) => {
        const item = document.createElement("span");
        item.className = "badge bg-label-danger me-2 mb-2 p-2"; // kırmızı ton daha uygun
        item.style.cursor = "default";

        item.innerHTML = `
            ${ch}
            <i class="bx bx-x ms-1"
               style="cursor:pointer;"
               onclick="removeChallenge(${index})"></i>
        `;

        container.appendChild(item);
    });
}
enableEnterToAdd("#ctChallengeInput", function (value) {
    challenges.push(value);
    renderChallenges();
});
// Silme
function removeChallenge(index) {
    challenges.splice(index, 1);
    renderChallenges();
}
//--------------------------------------- KEY LEARNING-----------------------//
// Learning listesi
let learnings = [];

// Add button
document.getElementById("btnAddLearning").addEventListener("click", () => {
    const input = document.getElementById("ctLearningInput");
    const value = input.value.trim();

    if (!value) return;

    learnings.push(value);
    renderLearnings();
    input.value = "";
});

// Listeyi render eden fonksiyon
function renderLearnings() {
    const container = document.getElementById("ctLearningList");
    container.innerHTML = "";

    learnings.forEach((l, index) => {
        const badge = document.createElement("span");
        badge.className = "badge bg-label-info me-2 mb-2 p-2"; // Learning için mavi ton
        badge.style.cursor = "default";

        badge.innerHTML = `
            ${l}
            <i class="bx bx-x ms-1" style="cursor:pointer;" onclick="removeLearning(${index})"></i>
        `;

        container.appendChild(badge);
    });
}
enableEnterToAdd("#ctLearningInput", function (value) {
    learnings.push(value);
    renderLearnings();
});
// Silme
function removeLearning(index) {
    learnings.splice(index, 1);
    renderLearnings();
}
//--------------------------- NEXT STEPS------------------------------//
// Next Step listesi
let nextSteps = [];

// Add button
document.getElementById("btnAddNextStep").addEventListener("click", () => {
    const input = document.getElementById("ctNextStepInput");
    const value = input.value.trim();

    if (!value) return;

    nextSteps.push(value);
    renderNextSteps();
    input.value = "";
});

// Liste render fonksiyonu
function renderNextSteps() {
    const container = document.getElementById("ctNextStepList");
    container.innerHTML = "";

    nextSteps.forEach((step, index) => {
        const badge = document.createElement("span");
        badge.className = "badge bg-label-warning me-2 mb-2 p-2"; // Next steps için sarı ton uygun
        badge.style.cursor = "default";

        badge.innerHTML = `
            ${step}
            <i class="bx bx-x ms-1" style="cursor:pointer;" onclick="removeNextStep(${index})"></i>
        `;

        container.appendChild(badge);
    });
}
enableEnterToAdd("#ctNextStepInput", function (value) {
    nextSteps.push(value);
    renderNextSteps();
});
// Silme
function removeNextStep(index) {
    nextSteps.splice(index, 1);
    renderNextSteps();
}

document.getElementById("btnTaskComplete").addEventListener("click", async () => {

    // TaskId modal açılırken set edilmişti
    const taskId = window.currentTaskId;
    if (!taskId) {
        showToast("Task ID could not be found", "error");
        return;
    }
    const notes = document.getElementById("ctCompletionNotes").value.trim();
    if (achievements.length === 0) {
        showToast("Please add at least 1 key achievement", "warning");
        return;
    }

    if (challenges.length === 0) {
        showToast("Please add at least 1 challenge faced", "warning");
        return;
    }

    if (learnings.length === 0) {
        showToast("Please add at least 1 key learning", "warning");
        return;
    }

    if (!notes) {
        showToast("Please enter completion notes", "warning");
        return;
    }


    const payload = {
        slotId: taskId,
        achievements: achievements,
        challenges: challenges,
        learnings: learnings,
        nextSteps: nextSteps,
        notes: notes,
        completedBy: window.getUserId(),
        createdBy: window.getUserName()
    };

    const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/CompleteTask`;

    const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
    });

    const result = await res.json();

    if (result.errors != null && result.errors.length>0) {
        showToast(result.errors?.join("<br>") || "Task could not be completed", "error");
        return;
    }

    showToast("Task completed successfully!", "success");

    // Modalı kapat
    const modalEl = document.getElementById("closeTaskModal");
    const instance = bootstrap.Modal.getInstance(modalEl);
    instance?.hide();

    const modalTaskDetailEl = document.getElementById("taskDetailModal");
    const instanceTaskDetail = bootstrap.Modal.getInstance(modalTaskDetailEl);
    instanceTaskDetail?.hide();


    // Takvimi yenile
    if (typeof calendar !== "undefined") calendar.refetchEvents();

    // Sidebar yenile
    if (typeof loadCalendarSidebar === "function") loadCalendarSidebar();
});

let scheduleIdToDelete = null;
// DELETE BUTTON – MODAL AÇ
document.getElementById("btnDeleteSchedule").addEventListener("click", function () {

    scheduleIdToDelete = window.currentTaskId; // seçili schedule Id

    if (!scheduleIdToDelete) {
        showToast("Schedule is not found!", "error");
        return;
    }

    const deleteModal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
    deleteModal.show();
});

// CONFIRM DELETE BUTTON – API ÇAĞRISI
document.getElementById('confirmDeleteBtn').addEventListener('click', async function () {

    if (!scheduleIdToDelete) return;

    const userName = window.getUserName();

    try {
        const response = await fetch(`${window.ApiBaseUrl}/services/DitenPPM/Task/DeleteSchedule`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ slotId: scheduleIdToDelete, modifiedBy: userName })
        });

        const result = await response.json();

        if (result.data === true) {

            showToast("Schedule deleted successfully!", "success");

            // 🔄 Calendar refresh
            if (typeof calendar !== "undefined") {
                calendar.refetchEvents();
            }

            // Modal kapat
            bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();

            const modalTaskDetailEl = document.getElementById("taskDetailModal");
            const instanceTaskDetail = bootstrap.Modal.getInstance(modalTaskDetailEl);
            instanceTaskDetail?.hide();

        }
        else {
            showToast(result.errors || "Delete operation failed.", "error");
            console.warn(result.errors);
        }

    } catch (error) {
        console.error(error);
        showToast("An error occurred.", "error");
    }

    scheduleIdToDelete = null;
});

async function createRuntimeSlotAndRefresh(eventData, dropDate) {
    try {
        const payload = {
            taskId: eventData.id,
            start: dropDate.toISOString(),
            end: new Date(dropDate.getTime() + 60 * 60 * 1000).toISOString(),
            createdBy: window.getUserName()
        };

        const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/CreateRuntimeSlot`;

        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await res.json();

        if (result.errors && result.errors.length > 0) {
            showToast(result.errors.join("<br>") || "Drop failed", "error");
            return false;
        }

        showToast("Task added to calendar", "success");

        // Geçici event'leri sil
        calendar.getEvents().forEach(ev => {
            if (!ev.source || ev.source.id !== "fetchEvents") {
                ev.remove();
            }
        });

        // Eventleri yenile
        calendar.refetchEvents();

        return true;

    } catch (err) {
        console.error("CreateRuntimeSlot error:", err);
        showToast("Drop failed", "error");
        return false;
    }
}


document.addEventListener('DOMContentLoaded', async function () {
    const direction = isRtl ? 'rtl' : 'ltr';
    await loadCalendarSidebar();
    
  (function () {
      // DOM Elements
      loadUsers();

     


      if (typeof FullCalendar === 'undefined') {
          console.error('FullCalendar yüklenemedi!');
          return;
      }

      const { Calendar, Draggable } = FullCalendar; // ✅ Tema içindeki global FullCalendar'dan alınır
      const dayGridPlugin = window.FullCalendarDayGrid || FullCalendar?.dayGridPlugin;
      const interactionPlugin = window.FullCalendarInteraction || FullCalendar?.interactionPlugin;
      const listPlugin = window.FullCalendarList || FullCalendar?.listPlugin;
      const timegridPlugin = window.FullCalendarTimeGrid || FullCalendar?.timeGridPlugin;
      const calendarEl = document.getElementById('calendar');
      const containerEl = document.getElementById('external-tasks');

     
      


    const appCalendarSidebar = document.querySelector('.app-calendar-sidebar');
    const addEventSidebar = document.getElementById('addEventSidebar');
    const appOverlay = document.querySelector('.app-overlay');
    const offcanvasTitle = document.querySelector('.offcanvas-title');
    const btnToggleSidebar = document.querySelector('.btn-toggle-sidebar');
    const btnDeleteEvent = document.querySelector('.btn-delete-event');
    const btnCancel = document.querySelector('.btn-cancel');
    const eventTitle = document.getElementById('eventTitle');
    const eventStartDate = document.getElementById('eventStartDate');
    const eventEndDate = document.getElementById('eventEndDate');
    const eventUrl = document.getElementById('eventURL');
    const eventLocation = document.getElementById('eventLocation');
    const eventDescription = document.getElementById('eventDescription');
    const eventType = document.getElementById('eventType');
    const eventPriority = document.getElementById('eventPriority');
    const eventStatus = document.getElementById('eventStatus');

    const allDaySwitch = document.querySelector('.allDay-switch');
    const selectAll = document.querySelector('.select-all');
    const filterInputs = Array.from(document.querySelectorAll('.input-filter'));
    const inlineCalendar = document.querySelector('.inline-calendar');

     //Calendar settings
    const calendarColors = {
      Meeting: 'success'
      };



    // External jQuery Elements
    const eventLabel = $('#eventLabel'); // ! Using jQuery vars due to select2 jQuery dependency
    const eventGuests = $('#eventGuests'); // ! Using jQuery vars due to select2 jQuery dependency

    // Event Data
    let currentEvents = events; // Assuming events are imported from app-calendar-events.js
    let isFormValid = false;
    let eventToUpdate = null;
    //let inlineCalInstance = null;

    // Offcanvas Instance
    const bsAddEventSidebar = new bootstrap.Offcanvas(addEventSidebar);

    //! TODO: Update Event label and guest code to JS once select removes jQuery dependency
    // Initialize Select2 with custom templates
    if (eventLabel.length) {
      function renderBadges(option) {
        if (!option.id) {
          return option.text;
        }
        var $badge =
          "<span class='badge badge-dot bg-" + $(option.element).data('label') + " me-2'> " + '</span>' + option.text;

        return $badge;
      }
      eventLabel.wrap('<div class="position-relative"></div>').select2({
        placeholder: 'Select value',
        dropdownParent: eventLabel.parent(),
        templateResult: renderBadges,
        templateSelection: renderBadges,
        minimumResultsForSearch: -1,
        escapeMarkup: function (es) {
          return es;
        }
      });
    }



    // Event start (flatpicker)
    if (eventStartDate) {
      var start = eventStartDate.flatpickr({
        monthSelectorType: 'static',
        static: true,
        enableTime: true,
        altFormat: 'Y-m-dTH:i:S',
        onReady: function (selectedDates, dateStr, instance) {
          if (instance.isMobile) {
            instance.mobileInput.setAttribute('step', null);
          }
        }
      });
    }

    // Event end (flatpicker)
    if (eventEndDate) {
      var end = eventEndDate.flatpickr({
        monthSelectorType: 'static',
        static: true,
        enableTime: true,
        altFormat: 'Y-m-dTH:i:S',
        onReady: function (selectedDates, dateStr, instance) {
          if (instance.isMobile) {
            instance.mobileInput.setAttribute('step', null);
          }
        }
      });
    }

    // Inline sidebar calendar (flatpicker)
    //if (inlineCalendar) {
    //  inlineCalInstance = inlineCalendar.flatpickr({
    //    monthSelectorType: 'static',
    //    static: true,
    //    inline: true
    //  });
    //}

    // Event click function
   

    // Modify sidebar toggler
    function modifyToggler() {
      const fcSidebarToggleButton = document.querySelector('.fc-sidebarToggle-button');
      fcSidebarToggleButton.classList.remove('fc-button-primary');
      fcSidebarToggleButton.classList.add('d-lg-none', 'd-inline-block', 'ps-0');
      while (fcSidebarToggleButton.firstChild) {
        fcSidebarToggleButton.firstChild.remove();
      }
      fcSidebarToggleButton.setAttribute('data-bs-toggle', 'sidebar');
      fcSidebarToggleButton.setAttribute('data-overlay', '');
      fcSidebarToggleButton.setAttribute('data-target', '#app-calendar-sidebar');
      fcSidebarToggleButton.insertAdjacentHTML(
        'beforeend',
        '<i class="icon-base bx bx-menu icon-lg text-heading"></i>'
      );
    }

      // Takvimi başlat
       calendar = new FullCalendar.Calendar(calendarEl, {
          initialView: 'dayGridMonth',
          editable: true,       // takvimdeki event taşınabilir
          droppable: true,      // dışarıdan event bırakılabilir
           selectable: true,     // tarih seçilebilir
           eventDurationEditable: true,
           eventResizableFromStart: false,
           moreLinkContent: args => `+${args.num} more`,
            headerToolbar: {
              left: 'sidebarToggle, prev,next, title',
              right: 'dayGridMonth,timeGridWeek,timeGridDay,listMonth'
          },
          slotMinTime: "00:00:00",
           slotMaxTime: "24:00:00",
           firstDay:1,
          dayCellDidMount(info) {
              const today = new Date();
              today.setHours(0, 0, 0, 0);

              const cellDate = new Date(info.date);
              cellDate.setHours(0, 0, 0, 0);

              if (cellDate < today) {
                  info.el.style.backgroundColor = "#f5f5f5";
                  info.el.style.opacity = "0.5";
                  info.el.style.cursor = "not-allowed";
              }
          },
          eventAllow: function (dropInfo, draggedEvent) {
             
              if (draggedEvent.extendedProps.status ==='Completed') {
                  return false; 
              }


              const today = new Date();
              today.setHours(0, 0, 0, 0);

              // Drop edilecek event'in başlangıç tarihi
              const eventStart = dropInfo.start;   // Date objesi
              eventStart.setHours(0, 0, 0, 0);

              // Eğer eventStart < today ise izin verme
              return eventStart >= today;
          },
           eventClassNames: function ({ event: calendarEvent }) {
               const ext = calendarEvent.extendedProps;
               // Status öncelikli renk
               if (ext.status && ext.status.toLowerCase() === "completed") {
                   return ["bg-label-success"];
               }

               if (ext.status && ext.status.toLowerCase() === "cancelled") {
                   return ["bg-label-danger"];
               }
               // -------------------------------
               // 3) DUE DATE OVERDUE → DANGER
               // -------------------------------
               const endDate = ext.taskEndDate;
               if (endDate) {
                   const due = new Date(endDate);
                   const now = new Date();

                   if (due < now) {
                       return ["bg-label-danger"]; // overdue
                   }
               }

               // 4) Meeting ise calendarColors kullan
               const calendarType = calendarEvent._def.extendedProps.calendar;

               if (calendarType === "Meeting") {
                   const colorName = calendarColors[calendarType] || "success";
                   return ["bg-label-" + colorName];
               }

               // 5) Diğer tüm tasklar → secondary
               return ["bg-label-secondary"];
           },
           eventDragStart: function (info) {
               let event = info.event;

               const roundTo = 15; // 15 dakika

               function roundDate(date) {
                   let d = new Date(date);
                   let minutes = d.getMinutes();
                   let roundedMinutes = Math.round(minutes / roundTo) * roundTo;

                   if (roundedMinutes === 60) {
                       d.setHours(d.getHours() + 1);
                       roundedMinutes = 0;
                   }

                   d.setMinutes(roundedMinutes);
                   d.setSeconds(0);
                   d.setMilliseconds(0);
                   return d;
               }

               // START ve END ikisini de yuvarla
               const roundedStart = roundDate(event.start);
               const roundedEnd = roundDate(event.end);

               event.setStart(roundedStart);
               event.setEnd(roundedEnd);
           },

           events: fetchEvents,
           eventTimeFormat: {
               hour: '2-digit',
               minute: '2-digit',
               hour12: false
           },
           slotLabelFormat: {
               hour: '2-digit',
               minute: '2-digit',
               hour12: false
           },
           slotDuration: "00:15:00",
           snapDuration: "00:15:00",
           drop: async function (info) {

               const eventData = JSON.parse(info.draggedEl.dataset.event);
               await createRuntimeSlotAndRefresh(eventData, info.date);
           },
           eventContent: function (arg) {

               const start = arg.event.start;
               const end = arg.event.end;

               // 🕒 Saatleri 24 saat formatına çevirelim
               const startStr = start.toLocaleTimeString([], {
                   hour: '2-digit',
                   minute: '2-digit',
                   hour12: false
               });

               const endStr = end
                   ? end.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })
                   : "";

               // 📌 Örn: "14:00 - 15:00  Deneme Task"
               const timeRange = `${startStr}`;

               return {
                   html: `<div class="fc-event-time">${timeRange}</div>
               <div class="fc-event-title">${arg.event.title}</div>`
               };
           },

           eventAdd: function (info) {
               // Sidebar’dan sürüklenen event takvime otomatik eklenmesin
               // FullCalendar’ın otomatik oluşturduğu event budur → hemen sil
               if (!info.event.sourceId) {
                   info.event.remove();
               }
           },
           eventDrop: async function (info) {

               const slotId = info.event.id;           // SlotId
               const taskId = info.event.extendedProps.taskId; // TaskId
               const newStart = info.event.start;
               const newEnd = info.event.end;

               const payload = {
                   taskId: slotId,
                   start: newStart.toISOString(),
                   end: newEnd ? newEnd.toISOString() : newStart.toISOString(),
                   createdBy: window.getUserName()
               };

               const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/MoveRuntimeSlot`;

               try {
                   const response = await fetch(url, {
                       method: "POST",
                       headers: { "Content-Type": "application/json" },
                       body: JSON.stringify(payload)
                   });

                   const result = await response.json();

                   if (result.errors != null && result.errors.length>0) {
                       showToast(result.errors?.join("<br>") || "Event move failed", "error");
                       info.revert();   // ⛔ Kullanıcıyı eski yerine geri döndür
                       return;
                   }

                   showToast("Event moved", "success");

                   // 🔄 Calendar refresh
                   if (typeof calendar !== "undefined") {
                       calendar.refetchEvents();
                   }

                   // 🔄 Sidebar refresh
                   if (typeof loadCalendarSidebar === "function") {
                       loadCalendarSidebar();
                   }

               } catch (err) {
                   console.error(err);
                   showToast("Event move failed", "error");
                   info.revert();
               }
           },

           datesSet: function (arg) {
               if (arg.view.type === "dayGridMonth") {
                   calendar.setOption("height", "100vh");
                   calendar.setOption("expandRows", false);

                   // month görünümde sıkıştırma, +X more düzgün çalışır
                   calendar.setOption("dayMaxEvents", 4);
                   calendar.setOption("dayMaxEventRows", true);
                   return;
               }

               // DAY / WEEK VIEW
               if (arg.view.type === "timeGridDay" || arg.view.type === "timeGridWeek") {
                   calendar.setOption("height", "85vh");  // Tam ekran görünür
                   calendar.setOption("expandRows", true); // scroll açılır
                   calendar.setOption("slotMinTime", "00:00:00");
                   calendar.setOption("slotMaxTime", "24:00:00");
               }
              modifyToggler();
          },
          dateClick: function (info) {

              console.log("📅 Calendar date clicked:", info.date);

              // Tıklanan günü formatla
              let date = moment(info.date).format("YYYY-MM-DD");

              // Modalı tamamen temizle
              resetCreateTaskMeetingModal();  // <-- Bizim yazdığımız reset fonksiyonu

              // TYPE default olarak TASK olsun (istersen MEETING yaparım)
              $("#ddlType").val(1).trigger("change");

              // Task mode’da tarihleri set edelim
              $("#txt-start-date").val(date);
              $("#txt-end-date").val(date);

              // Meeting mode aktif olursa meeting picker da set olur
              $("#txt-meeting-start-date").val(date);
              $("#txt-meeting-end-date").val(date);

              // Modal başlığını set et
              $("#editUser .modal-title").text("New Task / Meeting");
              $('#ddlType').prop('disabled', false);
              // Modalı aç
              const modal = new bootstrap.Modal(document.getElementById("editUser"));
              modal.show();

              // Modal açılınca flatpickr + select2 initialize olsun
              setTimeout(() => {
                  initCreateTaskForm();     // dropdownlar
                  initMeetingPickers();     // meeting date pickers
                  fillMeetingTimes();       // 15dk time options
              }, 50);
          },
           eventClick: function (info) {

               const type = info.event.extendedProps.type;
               const status = info.event.extendedProps.status;
               if (type === 'Meeting' || status==='Completed') {
                   return;
               }

               openTaskDetailModal(info.event);
            },
           eventResize: async function (info) {
               const status = info.event.extendedProps.status;
               const type = info.event.extendedProps.type;

               if (status === 'Completed') {
                   info.revert();
                   showToast("Completed tasks cannot be resized.", "error");
                   return;
               }
               if (type === 'Meeting') {
                   info.revert();
                   showToast("Meetings cannot be resized.", "error");
                   return;
               }
               const newEnd = info.event.end;
               if (!newEnd) {
                   info.revert();
                   showToast("Invalid resize event.", "error");
                   return;
               }
               const formattedEnd = newEnd.toISOString();
               const newStart = info.event.start;
               const slotId = info.event.id;           // SlotId



               const payload = {
                   taskId: slotId,
                   start: newStart.toISOString(),
                   end: formattedEnd,
                   createdBy: window.getUserName()
               };

               const url = `${window.ApiBaseUrl}/services/DitenPPM/Task/MoveRuntimeSlot`;

               try {
                   const response = await fetch(url, {
                       method: "POST",
                       headers: { "Content-Type": "application/json" },
                       body: JSON.stringify(payload)
                   });

                   const result = await response.json();

                   if (result.errors != null && result.errors.length > 0) {
                       showToast(result.errors?.join("<br>") || "Event move failed", "error");
                       info.revert();   // ⛔ Kullanıcıyı eski yerine geri döndür
                       return;
                   }

                   showToast("Event duration updated successfully.");

                   // 🔄 Calendar refresh
                   if (typeof calendar !== "undefined") {
                       calendar.refetchEvents();
                   }


               } catch (err) {
                   console.error(err);
                   showToast("Event move failed", "error");
                   info.revert();
               }
          }
      });


      calendar.render();


   

    // Sidebar Toggle Btn
    if (btnToggleSidebar) {
      btnToggleSidebar.addEventListener('click', e => {
        btnCancel.classList.remove('d-none');
      });
    }


    // When modal hides reset input values
    addEventSidebar.addEventListener('hidden.bs.offcanvas', function () {
      resetValues();
    });

    // Hide left sidebar if the right sidebar is open
    btnToggleSidebar.addEventListener('click', e => {
      if (offcanvasTitle) {
        offcanvasTitle.innerHTML = 'Add Event';
      }
      btnDeleteEvent.classList.add('d-none');
      appCalendarSidebar.classList.remove('show');
      appOverlay.classList.remove('show');
    });

    // Jump to date on sidebar(inline) calendar change
    //inlineCalInstance.config.onChange.push(function (date) {
    //  calendar.changeView(calendar.view.type, moment(date[0]).format('YYYY-MM-DD'));
    //  modifyToggler();
    //  appCalendarSidebar.classList.remove('show');
    //  appOverlay.classList.remove('show');
    //});
  })();
});

// ================================
// 📅 Takvim Eventlerini Yükleme Modülü
// ================================




