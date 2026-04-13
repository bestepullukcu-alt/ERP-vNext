'use strict';
const eventGuests = $('#eventGuests');
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const port2 = protocol === 'https:' ? '5055' : '5050';
const port3 = protocol === 'https:' ? '5060' : '5053';
const userId = getUserId();
const userName = window.getUserName();

// Centralized state for form management (future proofing)
let TaskFormState = {
    currentTask: null,
    mode: "edit", // edit | readonly
    permissions: {
        canEditMainFields: false,
        canAddSchedule: false
    }
};

let isCreatingRuntimeSlot = false;
let externalDraggableInstance = null;

const readonlyTimeOptions = (function () {
    const opts = [];
    for (let h = 0; h < 24; h++) {
        for (let m = 0; m < 60; m += 15) {
            const hh = h.toString().padStart(2, '0');
            const mm = m.toString().padStart(2, '0');
            opts.push({ id: `${hh}:${mm}`, text: `${hh}:${mm}` });
        }
    }
    return opts;
})();

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
        const apiUrl = `${API.legacy.user}/api/PvUser/User/GetUsersByUserId/${userId}`;
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

async function loadAssignedToUsers() {
    const $select = $('#filterAssignedTo');
    if ($select.length === 0) return;

    if ($select.data('loaded')) return;

    try {
        const url = `${API.legacy.user}/api/PvUser/User/GetUsersByUserId/${userId}`;

        const response = await fetch(url);
        if (!response.ok) throw new Error("HTTP error: " + response.status);

        const result = await response.json();
        const users = result.data || [];

        $select.empty();
        $select.append('<option value=""></option>');
        $select.append('<option value="">All User</option>');

        users.filter(u => u.isActive === true).forEach(user => {
            $select.append(new Option(user.fullName, user.id));
        });

        if ($select.hasClass("select2-hidden-accessible")) {
            $select.select2("destroy");
        }

        const $offcanvas = $('#offcanvasTaskOverviewFilters');
        $select.select2({
            placeholder: $select.attr('data-placeholder') || 'All User',
            allowClear: true,
            dropdownParent: $offcanvas
        });


        $select.data('loaded', true);
    } catch (error) {
        console.error('Failed to load Assigned To users:', error);
    }
}

async function loadTaskStatuses() {
    const $select = $('#filterStatus');
    if ($select.length === 0) return;

    if ($select.data('loaded')) return;

    try {
        const url = `${API.ppm}/WorkflowCategory/GetWorkflowStatus`;

        const response = await fetch(url);
        if (!response.ok) throw new Error("HTTP error: " + response.status);

        const result = await response.json();
        const statuses = result || [];

        $select.empty();
        $select.append('<option value=""></option>');
        $select.append('<option value="">All Statuses</option>');

        statuses.forEach(status => {
            $select.append(new Option(status.name, status.id));
        });

        if ($select.hasClass("select2-hidden-accessible")) {
            $select.select2("destroy");
        }

        const $offcanvas = $('#offcanvasTaskOverviewFilters');
        $select.select2({
            placeholder: 'All Statuses',
            allowClear: true,
            multiple: true,
            dropdownParent: $offcanvas
        });

        $select.data('loaded', true);
    } catch (error) {
        console.error('Failed to load Task Statuses:', error);
    }
}

async function loadTaskPriorities() {
    const $select = $('#filterPriority');
    if ($select.length === 0) return;

    if ($select.data('loaded')) return;

    try {
        const url = `${API.ppm}/WorkflowCategory/GetPriorities`;

        const response = await fetch(url);
        if (!response.ok) throw new Error("HTTP error: " + response.status);

        const result = await response.json();
        const priorities = result || [];

        $select.empty();
        $select.append('<option value=""></option>');
        $select.append('<option value="">All Priority</option>');

        priorities.forEach(p => {
            $select.append(new Option(p.name, p.id));
        });

        if ($select.hasClass("select2-hidden-accessible")) {
            $select.select2("destroy");
        }

        const $offcanvas = $('#offcanvasTaskOverviewFilters');
        $select.select2({
            placeholder: 'All Priority',
            allowClear: true,
            multiple: true,
            dropdownParent: $offcanvas
        });

        $select.data('loaded', true);
    } catch (error) {
        console.error('Failed to load Task Priorities:', error);
    }
}

function renderDeadlineOption(option) {
    if (!option.id && option.text === "") return option.text;

    const icons = {
        "": "bx bx-time-five",
        "today": "bx bx-calendar-check",
        "tomorrow": "bx bx-calendar-plus",
        "this-week": "bx bx-calendar-week",
        "this-month": "bx bx-calendar-event",
        "overdue": "bx bx-error-circle text-danger"
    };

    const iconClass = icons[option.id] || "bx bx-calendar";
    return $(`<span><i class="${iconClass} me-2"></i>${option.text || 'All Time'}</span>`);
}

function initDeadlineFilter() {
    const $select = $('#filterDeadline');
    const $offcanvas = $('#offcanvasTaskOverviewFilters');

    if ($select.length === 0) return;

    if ($select.hasClass("select2-hidden-accessible")) {
        $select.select2("destroy");
    }

    $select.select2({
        placeholder: 'All Time',
        allowClear: true,
        dropdownParent: $offcanvas,
        templateResult: renderDeadlineOption,
        templateSelection: renderDeadlineOption,
        escapeMarkup: m => m
    });
}








//--------------------------------- CALENDAR SIDEBAR TASKS ---------------------------------//
let calendarDefaultFilter = {
    currentUserId: window.getUserId(),
    dueDateFilter: null,            // delayed | today | nextWeek | nextMonth
    priorityIds: null,              // [1,2,3,4]
    assignedFromUserIds: null,       // [ownerId]
    taskId: null
};

function initCalendarTaskFilter(tasks) {
    const $el = $('#calendarTaskFilter');
    if ($el.length === 0) return;

    const hasDefaultTask = !!calendarDefaultFilter.taskId;

    // Önce destroy
    if ($el.hasClass('select2-hidden-accessible')) {
        $el.select2('destroy');
    }

    // Rebuild options
    $el.empty().append('<option></option>');

    tasks.forEach(t => {
        $el.append(new Option(t.name, t.id, false, false));
    });

    // Init select2
    $el.select2({
        placeholder: "Search task...",
        allowClear: true,
        width: '100%',
        minimumInputLength: 0,
        dropdownParent: $el.parent()
    });

    // 🔥 Default task varsa set et
    if (hasDefaultTask) {
        $el.val(calendarDefaultFilter.taskId).trigger('change.select2');
    }

    // Change event
    $el.off('change').on('change', function () {
        const selectedTaskId = $(this).val();

        calendarDefaultFilter.taskId = selectedTaskId || null;

        applyCalendarTaskFilter(selectedTaskId);
    });
}

function applyCalendarTaskFilter(taskId) {

    // Sidebar refresh
    calendarDefaultFilter.taskId = taskId || null;
    loadCalendarSidebar();

    // Calendar refresh
    if (typeof calendar !== "undefined") {
        calendar.refetchEvents();
    }
}
async function fetchCalendarSidebarData(filter = calendarDefaultFilter) {

    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/CalendarSidebar`;
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ filter })
    });

    const json = await response.json();

    return {
        tasks: json.data?.tasks || [],
        meetings: json.data?.meetings || []
    };
}
document.getElementById("taskSort").addEventListener("change", async function () {
    const sortType = this.value;
    localStorage.setItem('calendarSidebarSort', sortType);

    const data = await fetchCalendarSidebarData(calendarDefaultFilter);

    const sortedTasks = sortTasks(data.tasks, sortType);
    const sortedMeetings = sortTasks(data.meetings, sortType);

    renderSidebarTasks(sortedTasks);
    renderSidebarMeetings(sortedMeetings);
});
function sortTasks(list, sortType) {
    if (!Array.isArray(list)) return [];
    const today = new Date();

    return list.slice().sort((a, b) => {
        switch (sortType) {
            case "unplanned":
                const isUnplannedA = !a.scheduleItems || a.scheduleItems.length === 0;
                const isUnplannedB = !b.scheduleItems || b.scheduleItems.length === 0;
                if (isUnplannedA !== isUnplannedB) {
                    return isUnplannedA ? -1 : 1;
                }
                return (a.priorityId || 999) - (b.priorityId || 999);

            case "recent":
                const dateA_recent = a.createdDate ? new Date(a.createdDate) : new Date(0);
                const dateB_recent = b.createdDate ? new Date(b.createdDate) : new Date(0);
                return dateB_recent - dateA_recent;

            case "priority_desc":
                return (a.priorityId || 999) - (b.priorityId || 999);

            case "overdue":
                const endA = a.endDate ? new Date(a.endDate) : null;
                const endB = b.endDate ? new Date(b.endDate) : null;
                const isOverdueA = !!(endA && endA < today);
                const isOverdueB = !!(endB && endB < today);
                if (isOverdueA !== isOverdueB) {
                    return isOverdueA ? -1 : 1;
                }
                return (endA || new Date("2100-01-01")) - (endB || new Date("2100-01-01"));

            case "deadline":
                const dlA = a.endDate ? new Date(a.endDate) : new Date("2100-01-01");
                const dlB = b.endDate ? new Date(b.endDate) : new Date("2100-01-01");
                return dlA - dlB;

            // Backwards compatibility
            case "priority":
                return (a.priorityId || 999) - (b.priorityId || 999);
            case "status":
                return (a.statusId || 999) - (b.statusId || 999);
            case "date":
                const dateA = new Date(a.startDate || a.endDate || a.startDateTime || a.dueDate || "2100-01-01");
                const dateB = new Date(b.startDate || b.endDate || b.startDateTime || b.dueDate || "2100-01-01");
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
    resetMeetingDetailModal();
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetMeetingDetail/${meetingId}`;

    const response = await fetch(url, {
        method: "GET",
        headers: { "Content-Type": "application/json" }
    });

    const data = await response.json();
    const m = data.data;

    // ACTION BUTTONS: Disable if ended
    updateMeetingActionButtons(m.startDate, m.endDate);

    // SET FIELDS
    document.getElementById("mdTitle").innerText = m.name;
    document.getElementById("mdDescription").innerText = renderMeetingDescription(m.description);
    document.getElementById("mdOwnerName").innerText = m.ownerName;

    document.getElementById("mdDate").innerText =
        moment(m.startDate).format("DD.MM.YYYY");

    document.getElementById("mdTime").innerText =
        moment(m.startDate).format("HH:mm") +
        " - " +
        moment(m.endDate).format("HH:mm");

    document.getElementById("mdLocation").innerText =
        m.location || "-";

    renderMeetingLink(m.meetingLink);

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
    const url = `${API.ppm}/services/DitenPPM/Task/RespondMeeting`;

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

/**
 * Resets all dynamic fields in the Meeting Detail Modal to prevent stale data.
 */
function resetMeetingDetailModal() {
    console.log("🔄 Resetting Meeting Detail Modal...");

    // Text fields
    $("#mdTitle").text("");
    $("#mdDescription").text("");
    $("#mdOwnerName").text("");
    $("#mdDate").text("");
    $("#mdTime").text("");
    $("#mdLocation").text("");

    // Link field
    const $link = $("#mdLink");
    $link.attr("href", "#").html("-").show();

    // Containers and Counters
    $("#mdAttendeeCount").text("0");
    $("#mdAttendees").empty();
    $("#mdAgenda").empty();

    // Action Buttons
    const $actionBtns = $("#btnAccept, #btnMaybe, #btnDecline");
    $actionBtns.off('click').prop('disabled', false).show();
    $actionBtns.each(function () { this.onclick = null; });

    // Meta / Status / Notices (Future proofing)
    $("#mdStatusBadge, #mdPermissionNotice").addClass("d-none");
    $("#mdNoticeText").text("");
    $("#mdMetaStrip").html("");

    // Ensure modal height/scroll is reset if needed
    $("#meetingDetailModal .modal-body").scrollTop(0);
}

/**
 * Disables meeting action buttons (Accept/Maybe/Decline) if the meeting has already ended.
 * @param {string|Date|Moment} startDate
 * @param {string|Date|Moment} endDate
 */
function updateMeetingActionButtons(startDate, endDate) {
    const end = moment(endDate);
    const now = moment();
    const $btns = $("#btnAccept, #btnMaybe, #btnDecline");

    if (now.isAfter(end)) {
        // Option 1: Disable
        $btns.prop('disabled', true).addClass('opacity-50');
        // Option 2: Hide (as per user choice, choosing disable with opacity for clarity)
        // $btns.hide();
        console.log("📅 Meeting ended, disabling action buttons.");
    } else {
        $btns.prop('disabled', false).removeClass('opacity-50').show();
    }
}

/**
 * Safely renders the meeting description by stripping HTML tags.
 * @param {string} description 
 * @returns {string}
 */
function renderMeetingDescription(description) {
    if (!description || description.trim() === "") {
        return "No description provided.";
    }

    // Create a temporary element to strip HTML tags
    const tempDiv = document.createElement("div");
    tempDiv.innerHTML = description;
    const plainText = tempDiv.textContent || tempDiv.innerText || "";

    return plainText.trim();
}

/**
 * Unifies the rendering of the meeting link across different modal flows.
 * @param {string} link 
 */
function renderMeetingLink(link) {
    const $link = $("#mdLink");

    if (link && link.trim() !== "" && link !== "-") {

        let finalLink = link.trim();

        // Eğer http / https yoksa otomatik ekle
        if (!/^https?:\/\//i.test(finalLink)) {
            finalLink = "https://" + finalLink;
        }

        $link
            .attr("href", finalLink)
            .attr("target", "_blank")
            .removeClass("text-muted")
            .css("pointer-events", "auto")
            .html(`<i class="bx bx-video me-1"></i> Join Meeting`);

        $link.closest('.alert-success').show();

    } else {
        $link
            .attr("href", "javascript:void(0)")
            .removeAttr("target")
            .addClass("text-muted")
            .css("pointer-events", "none")
            .html(`<i class="bx bx-video-off me-1"></i> No Link Provided`);
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
    container.innerHTML = "";

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    tasks.forEach(t => {
        const div = document.createElement("div");

        // Priority Border Mapping
        let borderClass = 'border-primary';
        switch (Number(t.priorityId)) {
            case 1: borderClass = 'border-primary'; break;
            case 2: borderClass = 'border-info'; break;
            case 3: borderClass = 'border-warning'; break;
            case 4: borderClass = 'border-danger'; break;
            default: borderClass = 'border-secondary'; break;
        }

        let avatarColor = 'bg-label-secondary';
        switch (Number(t.priorityId)) {
            case 1: avatarColor = 'bg-label-primary'; break;
            case 2: avatarColor = 'bg-label-info'; break;
            case 3: avatarColor = 'bg-label-warning'; break;
            case 4: avatarColor = 'bg-label-danger'; break;
            default: avatarColor = 'bg-label-secondary'; break;
        }

        // Days calculation
        let dueText = "-";
        let overdueClass = "text-muted";
        if (t.endDate) {
            const endDate = new Date(t.endDate);
            endDate.setHours(0, 0, 0, 0);
            const diffTime = endDate.getTime() - today.getTime();
            const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

            const formattedDate = moment(t.endDate).format("DD.MM.YY");

            if (diffDays < 0) {
                dueText = `${formattedDate} · ${Math.abs(diffDays)} days overdue`;
                overdueClass = "text-danger fw-medium";
            } else if (diffDays === 0) {
                dueText = `${formattedDate} · Due today`;
                overdueClass = "text-warning fw-medium";
            } else {
                dueText = `${formattedDate} · ${diffDays} days left`;
                overdueClass = "text-muted";
            }
        }

        // Status Badge Logic
        const statusLabel = t.statusName || (t.statusId == 1 ? "To Do" : t.statusId == 2 ? "In Progress" : "New");
        const statusColor = t.statusId == 2 ? "primary" : "secondary";

        // Card Container with Bootstrap Classes
        div.className = `card shadow-none bg-label-secondary border-start ${borderClass} fc-event fc-h-event fc-daygrid-event fc-daygrid-block-event mb-3 pointer p-0`;

        div.dataset.taskId = t.id;
        div.dataset.event = JSON.stringify({
            id: t.id,
            title: t.name,
            end: t.endDate,
            toUserId: calendarDefaultFilter.currentUserId,
            estimatedHour: t.estimatedHour
            //estimatedMinutes: t.estimatedMinutes
        });

        div.innerHTML = `
            <div class="card-body p-3">
                <div class="d-flex flex-column gap-1">
                    <div class="d-flex justify-content-between align-items-start">
                        <h6 class="mb-0 fw-bold text-heading text-truncate" style="max-width: 170px;" title="${t.name}">${t.name}</h6>
                        <span class="badge bg-label-${statusColor} badge-sm rounded-pill px-2 py-1" style="font-size: 0.7rem;">${statusLabel}</span>
                    </div>
                    
                    <div class="d-flex align-items-center gap-2 mt-1">
                        <div class="avatar-group d-flex align-items-center">
                            <div class="avatar avatar-xs" data-bs-toggle="tooltip" title="Owner: ${t.ownerName}">
                                <span class="avatar-initial rounded-circle ${t.avatarColor}" style="font-size: 0.6rem;">${(t.ownerName || 'U').substring(0, 2).toUpperCase()}</span>
                            </div>
                        </div>
                        <div class="d-flex flex-column">
                            <div class="d-flex align-items-center gap-1 small ${overdueClass}" style="font-size: 0.75rem;">
                                <i class="bx bx-calendar-event" style="font-size: 0.85rem;"></i>
                                <span>${dueText}</span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        container.appendChild(div);
    });

    // Re-init tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
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
    if (externalDraggableInstance) {
        console.log("FullCalendar.Draggable already initialized.");
        return;
    }

    const containerEl = document.getElementById("external-tasks");
    if (!containerEl) return;

    externalDraggableInstance = new FullCalendar.Draggable(containerEl, {
        itemSelector: ".fc-event",
        eventData: function (eventEl) {
            const data = JSON.parse(eventEl.dataset.event);
            return {
                title: eventEl.innerText.trim(),
                extendedProps: {
                    toUserId: data.toUserId,
                    status: data.status,
                    estimatedHour: data.estimatedHour
                    //estimatedMinutes: data.estimatedMinutes
                },
                backgroundColor: window.getComputedStyle(eventEl).backgroundColor,
                borderColor: window.getComputedStyle(eventEl).backgroundColor,
                textColor: window.getComputedStyle(eventEl).color
            };
        }
    });
}
async function loadCalendarSidebar() {
    const { tasks, meetings } = await fetchCalendarSidebarData(calendarDefaultFilter);

    // 🔥 Populate Search Filter (Only populate if filter is NOT active to keep full list)
    initCalendarTaskFilter(tasks);

    // 🔍 CLIENT-SIDE FILTERING IF TASK ID IS SELECTED
    let filteredTasks = tasks;
    let filteredMeetings = meetings;

    if (calendarDefaultFilter.taskId) {
        filteredTasks = tasks.filter(t => t.id === calendarDefaultFilter.taskId);
        // If searching for a task, usually we hide meetings or filter them too. 
        // Let's filter meetings by ID too just in case, or clear them if strictly task search.
        // For now, let's filter both by ID to be safe and consistent.
        filteredMeetings = meetings.filter(m => m.id === calendarDefaultFilter.taskId);
    }

    // Get persisted sort
    const sortType = localStorage.getItem('calendarSidebarSort') || 'unplanned';
    const sortDropdown = document.getElementById("taskSort");
    if (sortDropdown) {
        sortDropdown.value = sortType;
    }

    const sortedTasks = sortTasks(filteredTasks, sortType);
    const sortedMeetings = sortTasks(filteredMeetings, sortType);

    renderSidebarTasks(sortedTasks);
    renderSidebarMeetings(sortedMeetings);
    initTaskDraggables();
}
//--------------------------------- END CALENDAR SIDEBAR TASKS ---------------------------------//

//----------------------------------- ADD EVENT MODAL -----------------------------------//
async function loadCreateTaskTypes() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskTypes`;
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
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`;
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
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetPriorities`;
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


async function loadCreateTaskWorkflows() {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Workflow/GetWorkflows`;
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

$(document).on("click", ".create-task", async function () {
    window.isEditMode = false;
    $("#taskCreateModal .modal-title").text("Create Task");
    $(".btn-save-task")
        .attr("data-mode", "create")
        .removeAttr("data-id")
        .text("Save Task");

    await initTaskModalForm();
    initScheduleSystem($("#taskCreateModal"), false); // Create mode: Reset but don't add automatic item

    // Clear form
    $('#taskCreateForm')[0].reset();
    $('#taskCreateModal .select2').val(null).trigger('change');
    $('#ddlType').val(1); // Ensure type is Task

    // Reset Quill
    if (window.taskDescriptionQuill) {
        window.taskDescriptionQuill.setText('');
    }

    const modal = new bootstrap.Modal(document.getElementById("taskCreateModal"));
    modal.show();
});

$(document).on("click", ".create-meeting", async function () {
    window.isEditMode = false;
    $("#meetingCreateModal .modal-title").text("Create Meeting");
    $(".btn-save-meeting")
        .attr("data-mode", "create")
        .removeAttr("data-id")
        .text("Save Meeting");
    $('.btn-delete-meeting').addClass('d-none');
    // Clear form
    $('#meetingCreateForm')[0].reset();
    $('#meetingCreateModal .select2').val(null).trigger('change');

    await initMeetingModalForm();



    const modal = new bootstrap.Modal(document.getElementById("meetingCreateModal"));
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
        loadCreateTaskWorkflows(),
        loadCreateTaskStatus(),
        loadCreateTaskPriority(),
        fillMeetingTimes()
    ]);

    console.log("✔ Dropdown verileri yüklendi, select2 init ediliyor...");

    // 2) Select2 init (SADECE BURADA)
    // 2) Select2 init (SADECE BURADA)
    $('#taskCreateModal .select2').select2({
        dropdownParent: $('#taskCreateModal'),
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
        console.error("Task ID not found");
        return;
    }

    console.log("Sidebar task clicked →", taskId);
    await openTaskActionOrchestrator(taskId);
});

async function openTaskActionOrchestrator(taskId) {
    const url = `${API.ppm}/Task/GetTaskDetailById/${taskId}`;

    try {
        const res = await fetch(url);
        const result = await res.json();

        if (!result?.data) {
            console.error("Task not found:", result);
            return;
        }

        const task = result.data;
        const loggedInUserId = getUserId();

        // Centralize task state
        TaskFormState.currentTask = task;

        // 1) Handle Meeting separately (Keep existing edit flow as per request)
        if (task.typeId === 2) {
            isEditMode = true;
            TaskFormState.mode = "edit";
            await populateEditMeetingForm(task);
            $("#meetingCreateModal .modal-title").text("Edit Meeting");
            $(".btn-save-meeting").attr("data-mode", "edit").attr("data-id", task.id).text("Update Meeting");
            new bootstrap.Modal(document.getElementById('meetingCreateModal')).show();
            return;
        }

        // 2) Role-based decision for Tasks
        const isOwner = task.ownerId === loggedInUserId;

        if (isOwner) {
            // User is OWNER → Open Edit Task Modal
            isEditMode = true;
            TaskFormState.mode = "edit";

            await populateEditTaskForm(task);
            $("#taskCreateModal .modal-title").text("Edit Task");
            $(".btn-save-task").attr("data-mode", "edit").attr("data-id", task.id).text("Update Task");
            new bootstrap.Modal(document.getElementById('taskCreateModal')).show();
        } else {
            // User is NOT OWNER → Open Readonly Modal (#taskReadonlyModal)
            TaskFormState.mode = "readonly";

            await openTaskReadonlyModal(task);
        }

    } catch (err) {
        console.error("openTaskActionOrchestrator error:", err);
    }
}

async function openTaskEditModal(taskId) {
    await openTaskActionOrchestrator(taskId);
}

async function populateEditTaskForm(task) {
    const $modal = $("#taskCreateModal").length ? $("#taskCreateModal") : $("#editUser");

    await initTaskModalForm();
    initScheduleSystem($modal, false); // Edit mode: Reset but don't add automatic item

    // Common
    $modal.find('#txt-name').val(task.name);
    // Set Quill content
    if (window.taskDescriptionQuill) {
        window.taskDescriptionQuill.root.innerHTML = task.description || '';
    }

    $modal.find('#ddlCategory').val(task.categId).trigger('change');
    $modal.find('#ddlWorkflow').val(task.workflowId).trigger('change');

    setTimeout(() => {
        $modal.find('#ddlAssignee').val(task.assigneeIds[0]).trigger('change');
    }, 50);

    $modal.find('#ddlStatus').val(task.statusId).trigger("change");
    $modal.find('#ddlPriority').val(task.priorityId).trigger("change");

    if (task.startDate) {
        const startDate = task.startDate.split("T")[0];
        $modal.find("#txt-start-date").val(startDate);
        const fpStart = $modal.find("#txt-start-date")[0]?._flatpickr;
        if (fpStart) fpStart.setDate(startDate);
    }

    if (task.endDate) {
        const endDate = task.endDate.split("T")[0];
        $modal.find("#txt-end-date").val(endDate);
        const fpEnd = $modal.find("#txt-end-date")[0]?._flatpickr;
        if (fpEnd) fpEnd.setDate(endDate);
    }

    $modal.find('#txt-estimated-hour').val(task.estimatedHour);
    if (task.scheduleItems && task.scheduleItems.length > 0) {
        populateScheduleForEdit(task.scheduleItems, $modal);
    }
}
let isPopulatingSchedule = false;

function utcDateTimeToLocalParts(dateStr, timeStr, isAllDay) {
    // dateStr: "2026-01-26"
    // timeStr: "03:00" (UTC)

    if (!dateStr) return { date: null, time: null };

    if (isAllDay) {
        // All-day UTC günü local’e çevir
        const utc = new Date(`${dateStr}T00:00:00Z`);
        return {
            date: utc.toLocaleDateString('en-CA'), // YYYY-MM-DD
            time: null
        };
    }

    if (!timeStr) return { date: dateStr, time: null };

    const utc = new Date(`${dateStr}T${timeStr}:00Z`);

    const localDate = utc.toLocaleDateString('en-CA'); // YYYY-MM-DD
    const localTime = utc.toTimeString().slice(0, 5);  // HH:mm

    return {
        date: localDate,
        time: localTime
    };
}

async function populateScheduleForEdit(scheduleItemsResponse, $modal) {
    if (!$modal || $modal.length === 0) {
        $modal = $("#taskCreateModal").length ? $("#taskCreateModal") : $("#editUser");
    }

    resetScheduleState($modal);

    if (!Array.isArray(scheduleItemsResponse) || scheduleItemsResponse.length === 0)
        return;

    isPopulatingSchedule = true;

    for (const s of scheduleItemsResponse) {
        // Yeni schedule satırı oluştur
        addScheduleItem($modal);

        const id = scheduleCounter;

        // Elemanları Modal Scope içinde ara
        const $startDate = $modal.find(`#start-date-${id}`);
        if ($startDate.length === 0) continue;

        const $item = $startDate.closest(".schedule-item");
        if ($item.length === 0) continue;

        const startDateEl = $startDate[0];
        const endDateEl = $modal.find(`#end-date-${id}`)[0];
        const startTimeEl = $modal.find(`#start-time-${id}`)[0];
        const endTimeEl = $modal.find(`#end-time-${id}`)[0];
        const allDayEl = $item.find(".all-day-checkbox")[0];

        if (allDayEl) {
            allDayEl.checked = s.isAllDay === true;
        }

        const start = utcDateTimeToLocalParts(
            s.startDate,
            s.startTime,
            s.isAllDay
        );

        const end = utcDateTimeToLocalParts(
            s.endDate,
            s.endTime,
            s.isAllDay
        );

        // Tarihleri doldur
        const itemObj = scheduleItems.find(x => x.id === id);
        if (itemObj) {
            if (start.date) itemObj.startPicker.setDate(start.date, false);
            if (end.date) itemObj.endPicker.setDate(end.date, false);
        }

        // Saat alanları doldur
        if (!s.isAllDay) {
            if (start.time) $(startTimeEl).val(start.time).trigger("change.select2");
            if (end.time) $(endTimeEl).val(end.time).trigger("change.select2");
        } else {
            // All-day ise saatler gizlenmeli
            $(startTimeEl).val(null).trigger("change.select2");
            $(endTimeEl).val(null).trigger("change.select2");
        }

        // All day UI toggle
        toggleAllDay($item[0], s.isAllDay);

        // Validasyon tetikle
        validateSchedule(id, $modal);
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

function resetScheduleState($root) {
    if (!$root) {
        $("#scheduleList").empty();
    } else {
        $root.find("#scheduleList").empty();
    }
    scheduleItems = [];
    scheduleCounter = 0;
    if ($root) CreateScheduleValidator.toggleSaveButton($root, false);
}

/** --- Create/Edit Modal Schedule Validator Logic --- **/
const CreateScheduleValidator = {
    validateAll: function ($root) {
        if (!$root || $root.length === 0) return true;

        const $list = $root.find("#scheduleList");
        const $itemsAll = $list.find(".schedule-item");

        // 1) Handle 0 items case (Optional Schedule)
        if ($itemsAll.length === 0) {
            this.toggleSaveButton($root, false);
            this.toggleEmptyState($root, true);
            return true;
        }
        this.toggleEmptyState($root, false);

        let hasError = false;
        const $items = $itemsAll.not(".past-schedule");
        const scheduleData = [];

        $items.each((idx, el) => {
            const $item = $(el);
            const errors = this.validateItem($item);
            this.showErrors($item, errors);
            if (errors.length > 0) hasError = true;

            if (errors.length === 0) {
                scheduleData.push({
                    $el: $item,
                    start: this.getMomentFromItem($item, 'start'),
                    end: this.getMomentFromItem($item, 'end')
                });
            }
        });

        // 2) Overlap Check (Future only, and only if items are valid)
        if (!hasError && scheduleData.length > 1) {
            for (let i = 0; i < scheduleData.length; i++) {
                for (let j = i + 1; j < scheduleData.length; j++) {
                    const a = scheduleData[i];
                    const b = scheduleData[j];

                    if (a.start && a.end && b.start && b.end) {
                        if (a.start.isBefore(b.end) && b.start.isBefore(a.end)) {
                            hasError = true;
                            this.addOverlapError(a.$el, b.start, b.end);
                            this.addOverlapError(b.$el, a.start, a.end);
                        }
                    }
                }
            }
        }

        this.toggleSaveButton($root, hasError);
        return !hasError;
    },

    validateItem: function ($item) {
        const errors = [];
        const startVal = $item.find(".start-date").val();
        const endVal = $item.find(".end-date").val();
        const isAllDay = $item.find(".all-day-checkbox").is(":checked");

        // Schedule entries MUST have dates
        if (!startVal) errors.push("Start date is required");
        if (!endVal) errors.push("End date is required");

        if (!startVal || !endVal) return errors;

        const mStart = moment(startVal, "YYYY-MM-DD");
        const mEnd = moment(endVal, "YYYY-MM-DD");

        if (mStart.isAfter(mEnd, 'day')) {
            errors.push("Start date cannot be later than end date");
        } else if (mStart.isSame(mEnd, 'day') && !isAllDay) {
            const startTime = $item.find(".start-time").val();
            const endTime = $item.find(".end-time").val();

            // Time is required if not All Day
            if (!startTime) errors.push("Start time is required");
            if (!endTime) errors.push("End time is required");

            if (startTime && endTime) {
                const [sh, sm] = startTime.split(':').map(Number);
                const [eh, em] = endTime.split(':').map(Number);
                const sTotal = sh * 60 + sm;
                const eTotal = eh * 60 + em;
                if (sTotal >= eTotal) {
                    errors.push("Start time must be earlier than end time for same day");
                }
            }
        }
        return errors;
    },

    showErrors: function ($item, errors) {
        let $alertWrap = $item.find(".alert-wrapper");
        if (errors.length === 0) {
            $alertWrap.remove();
            $item.removeClass('border-danger');
            return;
        }

        if ($alertWrap.length === 0) {
            const html = `
                <div class="col-12 mt-3 alert-wrapper">
                    <div class="alert alert-danger alert-dismissible fade show mb-0" role="alert">
                        <div class="d-flex align-items-center mb-1">
                            <i class="bx bx-error me-2 fs-5 text-danger"></i>
                            <h6 class="alert-heading mb-0 fw-bold">Schedule Error</h6>
                        </div>
                        <div class="item-errors small opacity-90 ms-4 fw-bold"></div>
                    </div>
                </div>`;
            $item.append(html);
            $alertWrap = $item.find(".alert-wrapper");
        }
        $item.find(".item-errors").html(errors.join("<br>"));
        $item.addClass('border-danger');
    },

    addOverlapError: function ($item, otherStart, otherEnd) {
        const msg = `This schedule overlaps with another time slot (${otherStart.format('DD.MM HH:mm')} - ${otherEnd.format('DD.MM HH:mm')})`;
        let $errCont = $item.find(".item-errors");
        if ($errCont.length === 0) {
            this.showErrors($item, [msg]);
            return;
        }
        if ($errCont.text().indexOf("overlaps") === -1) {
            const current = $errCont.html();
            $errCont.html(current + (current ? "<br>" : "") + msg);
            $item.addClass('border-danger');
        }
    },

    getMomentFromItem: function ($item, type) {
        const dateVal = $item.find(`.${type}-date`).val();
        const timeVal = $item.find(`.${type}-time`).val();
        const isAllDay = $item.find(".all-day-checkbox").is(":checked");

        if (!dateVal) return null;
        let m = moment(dateVal, "YYYY-MM-DD");
        if (isAllDay) return type === 'start' ? m.startOf('day') : m.endOf('day');
        if (!timeVal) return null;
        const [h, min] = timeVal.split(':');
        return m.hour(h).minute(min).second(0);
    },

    toggleSaveButton: function ($root, hasError) {
        $root.find(".createTask, .btn-save-task").prop('disabled', hasError);
    },

    toggleEmptyState: function ($root, isEmpty) {
        const $list = $root.find("#scheduleList");
        let $placeholder = $list.find(".no-schedule-placeholder");
        if (isEmpty) {
            if ($placeholder.length === 0) {
                $list.append(`
                    <div class="no-schedule-placeholder text-center py-5 border border-dashed rounded bg-light">
                        <i class="bx bx-calendar-event fs-1 text-muted opacity-50 mb-2"></i>
                        <p class="text-muted mb-0">No specific schedule added. This is optional.</p>
                        <small class="text-muted">Click "Add Schedule" to plan time slots for this task.</small>
                    </div>`);
            }
        } else {
            $placeholder.remove();
        }
    }
};

function wireScheduleValidationForModal($modal) {
    $modal.off("change.schedule keyup.schedule", "#scheduleList input, #scheduleList select");
    $modal.on("change.schedule keyup.schedule", "#scheduleList input, #scheduleList select", function () {
        CreateScheduleValidator.validateAll($modal);
    });
}


function initScheduleSystem($root, autoAddItem = false) {
    if (!$root || $root.length === 0) return;
    resetScheduleState($root);

    if (autoAddItem) {
        addScheduleItem($root);
    } else {
        // Explicitly trigger validation to show empty state and enable buttons
        CreateScheduleValidator.validateAll($root);
    }
}
$(document).on("click", "#btnAddScheduleItem", function () {
    const $modal = $(this).closest('.modal');
    addScheduleItem($modal);
});


function addScheduleItem($modal) {
    if (!$modal || $modal.length === 0) return;
    scheduleCounter++;

    const template = $modal.find("#schedule-item-template")[0];
    const container = $modal.find("#scheduleList")[0];

    if (!template || !container) return;

    const newItem = template.content.cloneNode(true);
    const row = newItem.querySelector(".schedule-item");

    // inputları seç
    const startDate = row.querySelector(".start-date");
    const endDate = row.querySelector(".end-date");
    const startTime = row.querySelector(".start-time");
    const endTime = row.querySelector(".end-time");

    // unique ID ver
    startDate.id = `start-date-${scheduleCounter}`;
    endDate.id = `end-date-${scheduleCounter}`;
    startTime.id = `start-time-${scheduleCounter}`;
    endTime.id = `end-time-${scheduleCounter}`;

    container.appendChild(newItem);

    initScheduleItem(scheduleCounter, $modal);
    CreateScheduleValidator.validateAll($modal);
}

function initScheduleItem(id, $modal) {

    const startDateInput = document.getElementById(`start-date-${id}`);
    const endDateInput = document.getElementById(`end-date-${id}`);
    const startTimeInput = document.getElementById(`start-time-${id}`);
    const endTimeInput = document.getElementById(`end-time-${id}`);
    const row = startDateInput.closest(".schedule-item");

    // TIME DROPDOWNLARI
    fillTimeDropdown(startTimeInput);
    fillTimeDropdown(endTimeInput);

    // SELECT2
    $(startTimeInput).select2({
        dropdownParent: $modal,
        width: '100%'
    });
    $(endTimeInput).select2({
        dropdownParent: $modal,
        width: '100%'
    });

    const today = new Date().toISOString().split("T")[0];
    const minDate = isEditMode ? null : today;

    const startPicker = flatpickr(startDateInput, {
        dateFormat: "Y-m-d",
        minDate: minDate,
        disableMobile: true,
        allowInput: true,
        static: false,
        appendTo: document.body,
        position: "auto",
        onChange: function () { validateSchedule(id, $modal); }
    });

    const endPicker = flatpickr(endDateInput, {
        dateFormat: "Y-m-d",
        minDate: minDate,
        disableMobile: true,
        allowInput: true,
        static: false,
        appendTo: document.body,
        position: "auto",
        onChange: function () { validateSchedule(id, $modal); }
    });

    const validationTrigger = () => CreateScheduleValidator.validateAll($modal);

    $(startTimeInput).on("change", validationTrigger);
    $(endTimeInput).on("change", validationTrigger);

    const $allDay = $(row).find(".all-day-checkbox");
    $allDay.on("change", function (e) {
        toggleAllDay(row, e.target.checked);
        validationTrigger();
    });

    scheduleItems.push({ id, startPicker, endPicker });

    // REMOVE BUTTON
    const $removeBtn = $(row).find(".btnRemoveScheduleItem");
    $removeBtn.on("click", function (e) {
        if ($(row).hasClass("past-schedule")) {
            e.preventDefault();
            e.stopPropagation();
            return false;
        }
        removeScheduleItem(id, row);
        validationTrigger();
    });

    // Past Schedule Lock (Direct logic)
    const checkPast = () => {
        const startVal = $(startDateInput).val();
        const startTime = $(startTimeInput).val();
        const isAllDayVal = $allDay.is(":checked");

        if (startVal) {
            let m = moment(startVal, "YYYY-MM-DD");
            if (isAllDayVal) {
                m = m.startOf('day');
            } else if (startTime) {
                const [h, min] = startTime.split(':');
                m = m.hour(h).minute(min).second(0);
            } else {
                return;
            }

            if (m.isBefore(moment())) {
                $(row).addClass("past-schedule opacity-75");
                $(row).find("input, select").not($removeBtn).prop("disabled", true);
                if (startDateInput._flatpickr) startDateInput._flatpickr.destroy();
                if (endDateInput._flatpickr) endDateInput._flatpickr.destroy();

                $removeBtn.addClass("disabled opacity-50")
                    .attr("aria-disabled", "true")
                    .css({ pointerEvents: "auto", cursor: "not-allowed" });

                const btnEl = $removeBtn[0];
                const existing = bootstrap.Tooltip.getInstance(btnEl);
                if (existing) existing.dispose();

                new bootstrap.Tooltip(btnEl, {
                    title: 'Past schedule cannot be modified or removed',
                    placement: 'top',
                    container: $modal.attr('id') ? '#' + $modal.attr('id') : 'body',
                    trigger: 'hover focus'
                });
            }
        }
    };

    // Initial and deferred checks
    setTimeout(checkPast, 200);
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
        for (let m = 0; m < 60; m += 15) {
            const time = `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
            const option = document.createElement("option");
            option.value = time;
            option.text = time;
            el.appendChild(option);
        }
    }
}

function validateSchedule(id, $modal) {
    CreateScheduleValidator.validateAll($modal);
}
function clearCurrentScheduleRow(id, item) {
    item.startPicker.clear();
    item.endPicker.clear();

    const startTimeEl = document.getElementById(`start-time-${id}`);
    const endTimeEl = document.getElementById(`end-time-${id}`);

    if (startTimeEl) $(startTimeEl).val(null).trigger("change");
    if (endTimeEl) $(endTimeEl).val(null).trigger("change");
}

function collectScheduleItems($root) {
    const items = [];
    const $list = $root ? $root.find("#scheduleList") : $("#scheduleList");

    $list.find(".schedule-item").each(function () {

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

// --- Meeting Date & Time Sync & Validation ---
$(document).off('change', '#meeting-start-date').on('change', '#meeting-start-date', function () {
    const start = $(this).val();
    const $end = $('#meeting-end-date');
    const fpEnd = document.querySelector("#meeting-end-date")?._flatpickr;
    if (fpEnd) fpEnd.set('minDate', start);
    if (start && $end.val() && start > $end.val()) {
        $end.val(start);
        if (fpEnd) fpEnd.setDate(start);
    }
    syncMeetingTimes();
});

$(document).off('change', '#meeting-end-date').on('change', '#meeting-end-date', function () {
    const end = $(this).val();
    const $start = $('#meeting-start-date');
    const start = $start.val();
    if (start && end && end < start) {
        $start.val(end);
        const fpStart = document.querySelector("#meeting-start-date")?._flatpickr;
        if (fpStart) fpStart.setDate(end);
    }
    syncMeetingTimes();
});

function syncMeetingTimes() {
    const startDate = $('#meeting-start-date').val();
    const endDate = $('#meeting-end-date').val();
    if (startDate && endDate && startDate === endDate) {
        const startTime = $('#meeting-start-time').val();
        const endTime = $('#meeting-end-time').val();
        if (startTime && endTime && startTime >= endTime) {
            const [h, m] = startTime.split(':').map(Number);
            let nextM = m + 30;
            let nextH = h;
            if (nextM >= 60) { nextH++; nextM = 0; }
            if (nextH < 24) {
                const nextTime = `${nextH.toString().padStart(2, '0')}:${nextM.toString().padStart(2, '0')}`;
                $('#meeting-end-time').val(nextTime).trigger('change');
            } else {
                $('#meeting-end-time').val('23:30').trigger('change');
            }
        }
    }
}

$(document).off('change', '#meeting-start-time').on('change', '#meeting-start-time', syncMeetingTimes);
$(document).off('change', '#meeting-end-time').on('change', '#meeting-end-time', syncMeetingTimes);

function getNext30MinSlot() {
    const now = new Date();
    let m = now.getMinutes();
    let h = now.getHours();
    if (m > 30) { h++; m = 0; }
    else if (m > 0) { m = 30; }
    if (h === 24) h = 0;
    return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`;
}
//---------------------------- end Meeting Fields ----------------------------//

$('#editUser').on('hidden.bs.modal', function () {
    resetCreateTaskMeetingModal();
});

// Virtual toggle logic
$(document).off('change', '#meeting-virtual').on('change', '#meeting-virtual', function () {
    const isVirtual = $(this).is(':checked');
    const $locContainer = $('#container-meeting-location');
    const $linkContainer = $('#container-meeting-link');
    const $locInput = $('#meeting-location');
    const $linkInput = $('#meeting-link');

    if (isVirtual) {
        $locContainer.addClass('d-none');
        $linkContainer.removeClass('d-none');

        // MANUEL GİRİLEBİLSİN
        $linkInput.prop('readonly', false);
    } else {
        $linkContainer.addClass('d-none');
        $locContainer.removeClass('d-none');

        $linkInput.val('');
    }
});

// Reset Meeting Modal on close
$('#meetingCreateModal').on('hidden.bs.modal', function () {
    const $form = $('#meetingCreateForm');
    $form[0].reset();
    $form.find('.select2').val(null).trigger('change');
    $form.find('input, select, textarea').removeClass('is-invalid');

    // Reset Quill
    if (window.meetingDescriptionQuill) {
        window.meetingDescriptionQuill.setContents([]);
    }

    // Reset Containers
    $('#meeting-virtual').prop('checked', false).trigger('change');
    $('#meeting-owner').prop('disabled', false);
    window.isEditMode = false;
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
    resetScheduleState($('#editUser'));

    // 8) Type dropdownunu sıfırlama
    $('#ddlType').val(null).trigger('change');

    // 9) Hide delete button
    $('.btn-delete-meeting').addClass('d-none');
    window.isEditMode = false;

    console.log("✨ Modal tamamen sıfırlandı.");
}

// Modal Shown Event: Scoped Init & Validation Wire for both modals
$('#editUser, #taskCreateModal').on('shown.bs.modal', function () {
    const $modal = $(this);
    // Eğer create mode ise (data-id yoksa) init et
    if (!$modal.attr('data-id')) {
        initScheduleSystem($modal, false);
    }
    wireScheduleValidationForModal($modal);
    CreateScheduleValidator.validateAll($modal);
});

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
        const $modal = $("#editUser");
        if (!CreateScheduleValidator.validateAll($modal)) {
            showToast("Please fix schedule conflicts before saving.", "error");
            return;
        }
        payload.scheduleItems = collectScheduleItems($modal);
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
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/CreateTaskOrMeeting`;

        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await res.json();

        if (!result.errors || result.errors.length === 0) {
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
        const $modal = $("#editUser");
        if (!CreateScheduleValidator.validateAll($modal)) {
            showToast("Please fix schedule conflicts before saving.", "error");
            return;
        }
        payload.scheduleItems = collectScheduleItems($modal);
    }

    console.log("📦 UPDATE PAYLOAD:", payload);

    // ---------------------------------------------------
    // 6) API CALL
    // ---------------------------------------------------
    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/UpdateTaskOrMeeting`;

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
$(document).on("click", ".createTask", async function () {
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
        const apiUrl = `${API.ppm}/Task/GetCalendarEvents`;

        const payload = {
            ...calendarDefaultFilter
        };

        const response = await fetch(apiUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

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
                estimatedHour: ev.estimatedHour,
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

    btn.innerHTML = `<i class="icon-base bx bx-pause"></i> Stop Time Tracking`;

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

// Helper to check if actions like Delete or Close are allowed based on task date
// Rule: Destructive or completion actions are blocked ONLY for past tasks.
// Future and today's tasks are allowed.
function isTaskActionAllowed(event) {
    if (!event || !event.start) return false;

    const today = moment().startOf('day');
    const taskDate = moment(event.start).startOf('day');

    // Allow if task is today or in the future
    return taskDate.isSameOrAfter(today);
}

async function openTaskDetailModal(event) {
    // Reset button states first
    const $btnDelete = $('#btnDeleteSchedule');
    const $btnClose = $('#btnCloseTask');

    $btnDelete.prop('disabled', false).removeAttr('title');
    $btnClose.prop('disabled', false).removeAttr('title');

    // Check if actions are allowed (Blocking only past tasks)
    if (!isTaskActionAllowed(event)) {
        const disabledMsg = "This action cannot be performed on past tasks.";
        $btnDelete.prop('disabled', true).attr('title', disabledMsg);
        $btnClose.prop('disabled', true).attr('title', disabledMsg);
    }

    const taskId = event.extendedProps.taskId || event.id;
    window.currentTaskId = taskId;

    const url = `${API.ppm}/Task/GetTaskDetail/${taskId}`;

    const res = await fetch(url);
    const result = await res.json();

    if (result.errors != null && result.errors.length > 0) {
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

    document.getElementById("tdDescription").innerHTML =
        data.description || "<span class='text-muted'>-</span>";

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
                Start tracking using the <strong>Start Time Tracking</strong> button above.
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

/**
 * Handles clicks on meeting events.
 * Opens edit modal if user is owner and meeting hasn't ended.
 * Otherwise, opens a modern readonly detail modal.
 */
async function handleMeetingClick(event) {
    const meetingId = event.id;
    const currentUserId = getUserId();
    const now = moment();

    try {
        const url = `${API.ppm}/Task/GetTaskDetail/${meetingId}`;
        const res = await fetch(url);
        const result = await res.json();

        if (result.errors && result.errors.length > 0) {
            showToast("Meeting detail could not be loaded", "error");
            return;
        }

        const meeting = result.data;
        const isOwner = meeting.ownerId === currentUserId;
        const meetingEnd = moment(meeting.endDate);
        const hasEnded = meetingEnd.isBefore(now);

        // DECISION LOGIC
        if (isOwner && !hasEnded) {
            // OPEN EDIT MODAL (_CreateMeetingModal)
            // Reset modal first
            resetCreateTaskMeetingModal();

            // Set mode and ID on the save button
            $(".btn-save-meeting").attr("data-mode", "edit").attr("data-id", meeting.id);
            $(".modal-title").text("Edit Meeting");

            // GLOBAL EDIT FLAG
            window.isEditMode = true;

            await populateEditMeetingForm(meeting);

            const modal = new bootstrap.Modal(document.getElementById("meetingCreateModal"));
            modal.show();
        } else {
            // OPEN READ-ONLY MODAL (_MeetingDetailModal)
            openMeetingReadonlyModal(meeting, isOwner, hasEnded);
        }

    } catch (err) {
        console.error("handleMeetingClick error:", err);
        showToast("An error occurred while loading meeting details", "error");
    }
}

/**
 * Populates and opens the read-only Meeting Detail Modal.
 */
function openMeetingReadonlyModal(meeting, isOwner, hasEnded) {
    resetMeetingDetailModal();
    const $modal = $("#meetingDetailModal");

    // ACTION BUTTONS: Disable if ended
    updateMeetingActionButtons(meeting.startDate, meeting.endDate);

    // Title & Description
    $modal.find("#mdTitle").text(meeting.name);
    $modal.find("#mdDescription").text(renderMeetingDescription(meeting.description));

    // Status Badge & Notice
    const $statusBadge = $modal.find("#mdStatusBadge");
    const $notice = $modal.find("#mdPermissionNotice");
    const $noticeText = $modal.find("#mdNoticeText");

    if (hasEnded) {
        $statusBadge.removeClass("d-none").text("Ended");
        $notice.removeClass("d-none");
        $noticeText.text("This meeting has already ended.");
    } else if (!isOwner) {
        $statusBadge.addClass("d-none");
        $notice.removeClass("d-none");
        $noticeText.text("You don't have permission to edit this meeting.");
    } else {
        $statusBadge.addClass("d-none");
        $notice.addClass("d-none");
    }

    // Date & Time
    const start = moment(meeting.startDate);
    const end = moment(meeting.endDate);
    const now = moment();

    // 2️⃣ META STRIP: Relative Time
    const $metaStrip = $modal.find("#mdMetaStrip");
    let relativeText = "";

    if (now.isAfter(end)) {
        relativeText = `Ended ${end.fromNow()}`;
    } else if (now.isBefore(start)) {
        relativeText = `Starts ${start.fromNow()}`;
    } else {
        relativeText = `<span class="text-success fw-bold">Happening Now</span> • Ends ${end.fromNow()}`;
    }
    $metaStrip.html(relativeText);


    $modal.find("#mdDate").text(start.format("D MMMM YYYY"));
    $modal.find("#mdTime").text(`${start.format("HH:mm")} – ${end.format("HH:mm")}`);

    // Organizer
    const ownerName = meeting.ownerName || "Unknown";
    $modal.find("#mdOwnerName").text(ownerName);
    // Initials for avatar
    const initials = ownerName.split(" ").map(n => n[0]).join("").substring(0, 2).toUpperCase();
    $modal.find(".avatar-initial").text(initials);

    // Location / Link
    const $locContainer = $modal.find("#mdLocationContainer");
    const $linkContainer = $modal.find("#mdLinkContainer");

    if (meeting.isVirtual) {
        $locContainer.addClass("d-none");
        $linkContainer.removeClass("d-none");
        renderMeetingLink(meeting.meetingLink);
    } else {
        $locContainer.removeClass("d-none");
        $linkContainer.addClass("d-none");
        $modal.find("#mdLocation").text(meeting.location || "Not specified");
    }

    // Attendees (Rich Avatar Display)
    const $attendees = $modal.find("#mdAttendees");
    $attendees.empty();
    const count = meeting.assignees ? meeting.assignees.length : 0;
    $modal.find("#mdAttendeeCount").text(count > 0 ? `(${count})` : "");

    if (meeting.assignees && count > 0) {
        meeting.assignees.forEach(a => {
            const attInitials = a.name.split(" ").map(n => n[0]).join("").substring(0, 2).toUpperCase();
            // Jira-style attendee with avatar + name
            const attendeeHtml = `
                <div class="d-flex align-items-center">
                    <div class="avatar avatar-xs me-2">
                        <span class="avatar-initial rounded-circle bg-label-secondary text-dark fw-bold" style="font-size:0.65rem;">${attInitials}</span>
                    </div>
                    <span class="text-dark">${a.name}</span>
                </div>
            `;
            $attendees.append(attendeeHtml);
        });
    } else {
        $attendees.append("<span class='text-muted small'>No attendees invites.</span>");
    }

    // Modal Show
    const modalInstance = new bootstrap.Modal(document.getElementById("meetingDetailModal"));
    modalInstance.show();
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

    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/CreateTimesheetEntry`;

    const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
    });

    const result = await res.json();

    // ❌ Hatalı logic
    // if (result.errors == null && result.errors.length > 0)

    // ✔ Doğru error kontrolü
    if (result.errors != null && result.errors.length > 0) {
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
    btn.innerHTML = `<i class="icon-base bx bx-play"></i> Start Time Tracking`;
}
async function reloadTaskDetail(taskId) {
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskDetail/${taskId}`;
    const res = await fetch(url);
    const result = await res.json();

    if (result.errors != null && result.errors.length > 0) return;

    const d = result.data;

    // total
    document.getElementById("tdLogged").innerText = d.totalLogged;

    // logs list
    renderTimesheetList(d.timerSessions);
}

// --- COMPLETE TASK MODAL LOGIC (#closeTaskModal) ---
let ctAchievements = [];
let ctChallenges = [];
let ctLearnings = [];
let ctNextSteps = []; // Array of objects { name, type: 'task'|'meeting', data: {} }
let ctOutcomes = {
    files: [],
    text: { title: '', content: '' },
    link: ''
};

document.getElementById("btnCloseTask")?.addEventListener("click", () => {
    const taskId = window.currentTaskId;
    if (taskId) {
        window.location.href = `/Calendar/FinishTask?taskId=${taskId}`;
    }
});

async function openCloseTaskModal() {
    const taskId = window.currentTaskId;
    if (!taskId) return;

    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/GetTaskDetail/${taskId}`;
        const res = await fetch(url);
        const result = await res.json();

        if (result.errors && result.errors.length > 0) {
            showToast("Failed to load task details", "error");
            return;
        }

        initCompleteTaskModal(result.data);
    } catch (err) {
        console.error("Load task detail error:", err);
        showToast("Error loading task data", "error");
    }
}

function initCompleteTaskModal(taskData) {
    // Reset State
    ctAchievements = [];
    ctChallenges = [];
    ctLearnings = [];
    ctNextSteps = [];
    ctOutcomes = { files: [], text: { title: '', content: '' }, link: '' };

    // Set UI Fields
    document.getElementById("ctTaskNameHeader").innerText = taskData.name || "Untitled Task";
    document.getElementById("ctTimeSpent").innerText = taskData.totalLogged || "0m";
    document.getElementById("ctEstimated").innerText = taskData.estimatedHour || "0";
    document.getElementById("ctSessions").innerText = (taskData.timerSessions ? taskData.timerSessions.length : 0);

    // Efficiency
    const eff = calculateEfficiency(taskData.estimatedHour, taskData.totalLogged);
    const $effBadge = document.getElementById("ctEfficiency");
    $effBadge.innerText = eff.percent;
    $effBadge.className = `badge rounded-pill font-size-10 ${getEfficiencyColor(eff.score)}`;

    document.getElementById("ctEfficiencyMultiplier").innerText = `${eff.multiplier} Multiplier`;
    updateEfficiencyBar(eff.score);

    // Reset Forms
    document.getElementById("ctCompletionNotes").value = "";
    document.getElementById("ctAchievementInput").value = "";
    document.getElementById("ctChallengeInput").value = "";
    document.getElementById("ctLearningInput").value = "";
    document.getElementById("ctScheduleMeetingSwitch").checked = false;
    document.getElementById("ctMeetingPlanArea").classList.add("d-none");
    document.getElementById("ctOutcomeTextTitle").value = "";
    document.getElementById("ctOutcomeTextContent").value = "";
    document.getElementById("ctOutcomeLink").value = "";
    document.getElementById("ctUploadedFilesList").innerHTML = "";

    // Render Lists
    renderCtAchievements();
    renderCtChallenges();
    renderCtLearnings();
    renderCtNextSteps();
    renderCloseTaskSessions(taskData.timerSessions || []);

    // Init DatePickers if not done
    if (!document.getElementById("ctMeetingDate")._flatpickr) {
        flatpickr("#ctMeetingDate", { dateFormat: "d.m.Y", defaultDate: "today" });
        flatpickr("#ctMeetingTime", { enableTime: true, noCalendar: true, dateFormat: "H:i", time_24hr: true, defaultDate: "10:00" });
    }

    // Validation
    validateCompletionRules();

    // Show Modal
    const modal = new bootstrap.Modal(document.getElementById("closeTaskModal"));
    modal.show();
}

// --- Dynamic Validation ---
function validateCompletionRules() {
    const notes = document.getElementById("ctCompletionNotes").value.trim();
    const hasAchievements = ctAchievements.length > 0;
    const hasChallenges = ctChallenges.length > 0;
    const hasLearnings = ctLearnings.length > 0;
    const hasOutcome = ctOutcomes.files.length > 0 || (ctOutcomes.text.title && ctOutcomes.text.content) || ctOutcomes.link;
    const hasNextSteps = ctNextSteps.length > 0;

    // Hard Blocks (Mock items for demonstration)
    const hasOpenSubtasks = false; // Mock
    const hasUnfinishedChecklist = false; // Mock

    let status = {
        canComplete: true,
        isSoftBlocked: false,
        reasons: []
    };

    if (hasOpenSubtasks) {
        status.canComplete = false;
        status.reasons.push({ type: 'hard', msg: 'Task has open sub-tasks' });
    }
    if (hasUnfinishedChecklist) {
        status.canComplete = false;
        status.reasons.push({ type: 'hard', msg: 'Mandatory checklist items not completed' });
    }

    if (!notes) {
        status.isSoftBlocked = true;
        status.reasons.push({ type: 'soft', msg: 'Completion notes are missing' });
    }
    if (!hasAchievements) {
        status.isSoftBlocked = true;
        status.reasons.push({ type: 'soft', msg: 'No key achievements added' });
    }
    if (!hasOutcome) {
        status.isSoftBlocked = true;
        status.reasons.push({ type: 'soft', msg: 'Outcome / Deliverable is not provided' });
    }
    if (!hasNextSteps) {
        status.isSoftBlocked = true;
        status.reasons.push({ type: 'soft', msg: 'Next steps / Follow-ups not defined' });
    }

    updateValidationStatusUI(status);
    return status;
}

function updateValidationStatusUI(status) {
    const container = document.getElementById("ctValidationStatus");
    const btn = document.getElementById("btnTaskComplete");
    const softWarning = document.getElementById("softGuardWarning");

    container.innerHTML = "";

    if (status.reasons.length === 0) {
        container.innerHTML = `<div class="alert alert-label-success d-flex align-items-center mb-0" role="alert">
            <i class="bx bx-check-double me-2"></i> All requirements met. Ready to close.
        </div>`;
        btn.disabled = false;
        softWarning.classList.add("d-none");
        return;
    }

    const hardReasons = status.reasons.filter(r => r.type === 'hard');
    const softReasons = status.reasons.filter(r => r.type === 'soft');

    let html = "";
    if (hardReasons.length > 0) {
        html += `<div class="alert alert-label-danger mb-2">
            <h6 class="alert-heading fw-bold mb-1"><i class="bx bx-error-circle me-1"></i> Critical Blockers</h6>
            <ul class="ps-3 mb-0 small">
                ${hardReasons.map(r => `<li>${r.msg}</li>`).join('')}
            </ul>
        </div>`;
        btn.disabled = true;
        softWarning.classList.add("d-none");
    } else {
        btn.disabled = false;
        if (softReasons.length > 0) {
            html += `<div class="alert alert-label-warning mb-2">
                <h6 class="alert-heading fw-bold mb-1"><i class="bx bx-info-circle me-1"></i> Quality Recommendations</h6>
                <ul class="ps-3 mb-0 small">
                    ${softReasons.map(r => `<li>${r.msg}</li>`).join('')}
                </ul>
            </div>`;
            softWarning.classList.remove("d-none");
        } else {
            softWarning.classList.add("d-none");
        }
    }

    container.innerHTML = html;
}

// --- List Management ---
function renderCtAchievements() {
    const container = document.getElementById("ctAchievementsList");
    container.innerHTML = ctAchievements.map((ach, idx) => `
        <span class="badge bg-label-primary d-flex align-items-center py-2 px-3">
            ${ach}
            <i class="bx bx-x ms-2 pointer" onclick="removeCtAchievement(${idx})"></i>
        </span>
    `).join('');
}
function removeCtAchievement(idx) { ctAchievements.splice(idx, 1); renderCtAchievements(); validateCompletionRules(); }

function renderCtChallenges() {
    const container = document.getElementById("ctChallengesList");
    container.innerHTML = ctChallenges.map((ch, idx) => `
        <span class="badge bg-label-danger d-flex align-items-center py-2 px-3">
            ${ch}
            <i class="bx bx-x ms-2 pointer" onclick="removeCtChallenge(${idx})"></i>
        </span>
    `).join('');
}
function removeCtChallenge(idx) { ctChallenges.splice(idx, 1); renderCtChallenges(); validateCompletionRules(); }

function renderCtLearnings() {
    const container = document.getElementById("ctLearningList");
    container.innerHTML = ctLearnings.map((l, idx) => `
        <span class="badge bg-label-info d-flex align-items-center py-2 px-3">
            ${l}
            <i class="bx bx-x ms-2 pointer" onclick="removeCtLearning(${idx})"></i>
        </span>
    `).join('');
}
function removeCtLearning(idx) { ctLearnings.splice(idx, 1); renderCtLearnings(); validateCompletionRules(); }

function renderCtNextSteps() {
    const container = document.getElementById("ctNextStepListContainer");
    const emptyState = document.getElementById("nextStepEmptyState");

    if (ctNextSteps.length === 0) {
        emptyState.classList.remove("d-none");
        return;
    }
    emptyState.classList.add("d-none");

    container.innerHTML = ctNextSteps.map((step, idx) => `
        <div class="list-group-item list-group-item-action d-flex justify-content-between align-items-center border-0 px-0">
            <div class="d-flex align-items-center">
                <div class="avatar avatar-xs me-2">
                    <span class="avatar-initial rounded bg-label-${step.type === 'meeting' ? 'warning' : 'primary'}">
                        <i class="bx bx-${step.type === 'meeting' ? 'video' : 'check-circle'}"></i>
                    </span>
                </div>
                <div>
                    <h6 class="mb-0 small fw-bold">${step.name}</h6>
                    <small class="text-muted">Follow-up ${step.type}</small>
                </div>
            </div>
            <button class="btn btn-sm btn-icon border-0" onclick="removeCtNextStep(${idx})"><i class="bx bx-trash text-danger"></i></button>
        </div>
    `).join('');
}
function removeCtNextStep(idx) { ctNextSteps.splice(idx, 1); renderCtNextSteps(); validateCompletionRules(); }

// --- Event Listeners ---
$(document).on('click', '#btnAddAchievement', () => {
    const val = document.getElementById("ctAchievementInput").value.trim();
    if (val) { ctAchievements.push(val); renderCtAchievements(); document.getElementById("ctAchievementInput").value = ""; validateCompletionRules(); }
});

$(document).on('click', '#btnAddChallenge', () => {
    const val = document.getElementById("ctChallengeInput").value.trim();
    if (val) { ctChallenges.push(val); renderCtChallenges(); document.getElementById("ctChallengeInput").value = ""; validateCompletionRules(); }
});

$(document).on('click', '#btnAddLearning', () => {
    const val = document.getElementById("ctLearningInput").value.trim();
    if (val) { ctLearnings.push(val); renderCtLearnings(); document.getElementById("ctLearningInput").value = ""; validateCompletionRules(); }
});

$(document).on('change', '#ctCompletionNotes', () => validateCompletionRules());

$(document).on('click', '#btnAddNewStep', () => {
    const name = prompt("Enter next step task name:");
    if (name) {
        ctNextSteps.push({ name, type: 'task', data: { parentId: window.currentTaskId } });
        renderCtNextSteps();
        validateCompletionRules();
    }
});

$(document).on('change', '#ctScheduleMeetingSwitch', function () {
    const area = document.getElementById("ctMeetingPlanArea");
    if (this.checked) {
        area.classList.remove("d-none");
        // Auto-fill agenda
        const notes = document.getElementById("ctCompletionNotes").value;
        const taskName = document.getElementById("ctTaskNameHeader").innerText;
        document.getElementById("ctMeetingAgenda").value = `Discuss outcomes of: ${taskName}\n\nSummary: ${notes}`;
    } else {
        area.classList.add("d-none");
    }
    validateCompletionRules();
});

// Outcome Handlers
$(document).on('change', '#ctOutcomeFileInput', function (e) {
    const files = Array.from(e.target.files);
    files.forEach(f => {
        ctOutcomes.files.push(f);
        const html = `
            <div class="d-flex align-items-center p-2 mb-1 rounded ct-outcome-file-item small">
                <i class="bx bx-file me-2 text-primary"></i>
                <span class="flex-grow-1">${f.name}</span>
                <i class="bx bx-x text-danger pointer" onclick="removeOutcomeFile('${f.name}')"></i>
            </div>
        `;
        document.getElementById("ctUploadedFilesList").insertAdjacentHTML('beforeend', html);
    });
    validateCompletionRules();
});

function removeOutcomeFile(name) {
    ctOutcomes.files = ctOutcomes.files.filter(f => f.name !== name);
    // Refresh list UI (simple way)
    document.getElementById("ctUploadedFilesList").innerHTML = "";
    ctOutcomes.files.forEach(f => {
        const html = `<div class="d-flex align-items-center p-2 mb-1 rounded ct-outcome-file-item small"><i class="bx bx-file me-2 text-primary"></i><span class="flex-grow-1">${f.name}</span><i class="bx bx-x text-danger pointer" onclick="removeOutcomeFile('${f.name}')"></i></div>`;
        document.getElementById("ctUploadedFilesList").insertAdjacentHTML('beforeend', html);
    });
    validateCompletionRules();
}

$(document).on('input', '#ctOutcomeTextTitle, #ctOutcomeTextContent, #ctOutcomeLink', () => {
    ctOutcomes.text.title = document.getElementById("ctOutcomeTextTitle").value;
    ctOutcomes.text.content = document.getElementById("ctOutcomeTextContent").value;
    ctOutcomes.link = document.getElementById("ctOutcomeLink").value;
    validateCompletionRules();
});

// --- FINAL ACTION ---
document.getElementById("btnTaskComplete")?.addEventListener("click", handleCompleteAction);

async function handleCompleteAction() {
    const status = validateCompletionRules();
    if (!status.canComplete) return;

    const btn = document.getElementById("btnTaskComplete");
    const originalContent = btn.innerHTML;

    btn.disabled = true;
    btn.innerHTML = `<span class="spinner-border spinner-border-sm me-1" role="status"></span> Saving...`;

    const payload = {
        taskId: window.currentTaskId,
        notes: document.getElementById("ctCompletionNotes").value,
        achievements: ctAchievements,
        challenges: ctChallenges,
        learnings: ctLearnings,
        nextSteps: ctNextSteps,
        outcomes: {
            text: ctOutcomes.text,
            link: ctOutcomes.link,
            fileCount: ctOutcomes.files.length
        }
    };

    // Meeting Plan
    if (document.getElementById("ctScheduleMeetingSwitch").checked) {
        payload.meetingPlan = {
            agenda: document.getElementById("ctMeetingAgenda").value,
            date: document.getElementById("ctMeetingDate").value,
            time: document.getElementById("ctMeetingTime").value
        };
    }

    console.log("Submitting task completion:", payload);

    try {
        const result = await simulateApiCall(payload);
        if (result.success) {
            showToast("Task marked as completed and workspace saved.", "success");

            // Close modals
            bootstrap.Modal.getInstance(document.getElementById("closeTaskModal"))?.hide();
            bootstrap.Modal.getInstance(document.getElementById("taskDetailModal"))?.hide();

            // Refresh
            if (typeof calendar !== "undefined") calendar.refetchEvents();
            if (typeof loadCalendarSidebar === "function") loadCalendarSidebar();
        } else {
            showToast(result.message || "Failed to complete task", "error");
        }
    } catch (err) {
        showToast("An unexpected error occurred", "error");
    } finally {
        btn.disabled = false;
        btn.innerHTML = originalContent;
    }
}

function simulateApiCall(payload) {
    return new Promise((resolve) => {
        setTimeout(() => {
            console.log("Mock API Call Success:", payload);
            resolve({ success: true });
        }, 1500);
    });
}

function calculateEfficiency(estimatedHour, totalLoggedPretty) {
    const totalMinutes = parseDurationToMinutes(totalLoggedPretty);
    const estimatedMinutes = (estimatedHour || 0) * 60;

    if (estimatedMinutes <= 0 || totalMinutes <= 0) {
        return { percent: "0%", multiplier: "0x", score: 0 };
    }

    const efficiency = (estimatedMinutes / totalMinutes) * 100;
    const multiplier = (estimatedMinutes / totalMinutes).toFixed(1);

    return {
        percent: `${Math.round(efficiency)}%`,
        multiplier: `${multiplier}x`,
        score: efficiency
    };
}

function parseDurationToMinutes(str) {
    if (!str) return 0;
    let total = 0;
    const h = str.match(/(\d+)h/);
    const m = str.match(/(\d+)m/);
    const s = str.match(/(\d+)s/);
    if (h) total += parseInt(h[1]) * 60;
    if (m) total += parseInt(m[1]);
    if (s) total += parseInt(s[1]) / 60;
    return total;
}

function getEfficiencyColor(score) {
    if (score < 80) return "bg-label-danger";
    if (score < 110) return "bg-label-info";
    return "bg-label-success";
}

function updateEfficiencyBar(score) {
    const bar = document.getElementById("ctEfficiencyBar");
    const capped = Math.min(score, 100);
    bar.style.width = `${capped}%`;
    bar.className = `progress-bar progress-bar-striped progress-bar-animated ${getEfficiencyColor(score)}`;
}

function renderCloseTaskSessions(list) {
    const container = document.getElementById("ctTimerList");
    container.innerHTML = "";

    if (!list || list.length === 0) {
        container.innerHTML = `<div class="text-center py-3 text-muted">No sessions found</div>`;
        return;
    }

    list.forEach((s, i) => {
        container.innerHTML += `
            <div class="d-flex justify-content-between align-items-center p-2 rounded bg-lighter border-0 mb-2 small">
                <div>
                    <span class="badge bg-primary me-2 font-size-10">${i + 1}</span>
                    <span class="text-dark fw-medium">${s.endPretty || 'Unknown'}</span>
                </div>
                <div class="text-primary fw-bold">${s.duration}</div>
            </div>
        `;
    });
}


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
        const response = await fetch(`${protocol}//${domain}:${port}/services/DitenPPM/Task/DeleteSchedule`, {
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

function getEstimatedMinutes(eventOrProps) {
    if (!eventOrProps) return 0;
    // extendedProps check (FullCalendar event object) or direct object (dataset)
    const props = eventOrProps.extendedProps || eventOrProps;
    console.log(props);
    console.log(props.estimatedHour);
    let mins = parseInt(props.estimatedHour);
    if (!isNaN(mins) && mins > 0) return mins;

    let hours = parseFloat(props.estimatedHour);
    if (!isNaN(hours) && hours > 0) return Math.round(hours * 60);

    return 0;
}

function applyEstimatedEndTime(info) {
    const event = info.event;
    if (!event || !event.start) return { changed: false };

    const ext = event.extendedProps || {};
    if (ext.type === 'Meeting' || ext.calendar === 'Meeting') return { changed: false };

    const estimatedMinutes = getEstimatedMinutes(event);
    if (!estimatedMinutes || estimatedMinutes <= 0) return { changed: false };
    console.log(estimatedMinutes);
    const start = event.start;
    const newEnd = new Date(start.getTime() + estimatedMinutes * 60000);

    // 🔥 Tek hamlede: start + end + allDay=false
    event.setDates(start, newEnd, { allDay: false });

    return { changed: true, end: newEnd };
}


async function createRuntimeSlotAndRefresh(eventData, dropDate) {
    if (isCreatingRuntimeSlot) {
        console.warn("Already creating a runtime slot. Skipping duplicate call.");
        return false;
    }

    isCreatingRuntimeSlot = true;
    try {
        const estMinutes = getEstimatedMinutes(eventData);
        const durationMs = (estMinutes > 0 ? estMinutes : 60) * 60000;

        const payload = {
            taskId: eventData.id,
            start: dropDate.toISOString(),
            end: new Date(dropDate.getTime() + durationMs).toISOString(),
            createdBy: window.getUserName()
        };

        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/CreateRuntimeSlot`;

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
    } finally {
        isCreatingRuntimeSlot = false;
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
            if (!fcSidebarToggleButton) return;

            // Visual improvements
            fcSidebarToggleButton.classList.remove('fc-button-primary');
            fcSidebarToggleButton.classList.add('btn', 'btn-icon', 'd-lg-none', 'd-inline-block', 'ps-0');

            // Clear existing content
            while (fcSidebarToggleButton.firstChild) {
                fcSidebarToggleButton.firstChild.remove();
            }

            // Remove invalid Bootstrap attributes
            fcSidebarToggleButton.removeAttribute('data-bs-toggle');
            fcSidebarToggleButton.removeAttribute('data-overlay');
            fcSidebarToggleButton.removeAttribute('data-target');

            // Insert menu icon
            fcSidebarToggleButton.insertAdjacentHTML(
                'beforeend',
                '<i class="icon-base bx bx-menu icon-lg text-heading"></i>'
            );

            // Programmatic Sidebar Toggle
            // Cloning to remove old event listeners if any (FullCalendar might re-run modifyToggler)
            const newToggleButton = fcSidebarToggleButton.cloneNode(true);
            fcSidebarToggleButton.parentNode.replaceChild(newToggleButton, fcSidebarToggleButton);

            newToggleButton.addEventListener('click', (e) => {
                e.preventDefault();
                document.body.classList.toggle('sidebar-open');

                // If there's an overlay mechanism in the app, toggle it too
                const appOverlay = document.querySelector('.app-overlay');
                if (appOverlay) {
                    appOverlay.classList.toggle('show');
                }
            });
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
            firstDay: 1,
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
                const ext = draggedEvent.extendedProps;
                const assignedToId = draggedEvent.extendedProps?.toUserId; // Logged-in user checks against toUserId
                const loggedInUserId = getUserId();

                // 1) View-only check: Only users can modify their OWN tasks (assigned to them)
                if (assignedToId && assignedToId !== loggedInUserId) {
                    return false;
                }

                // 2) Completion check
                if (ext.status === 'Completed') {
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
                const classes = [];
                const loggedInUserId = getUserId();

                // 1) Read-only check: If not assigned to the current logged-in user
                if (ext.toUserId && ext.toUserId !== loggedInUserId) {
                    classes.push("fc-event-readonly");
                }

                // 2) Status based colors
                if (ext.status && ext.status.toLowerCase() === "completed") {
                    classes.push("bg-label-success");
                } else if (ext.status && ext.status.toLowerCase() === "cancelled") {
                    classes.push("bg-label-danger");
                } else if (ext.taskEndDate) {
                    // 3) Due date overdue → DANGER
                    const due = new Date(ext.taskEndDate);
                    const now = new Date();
                    if (due < now) {
                        classes.push("bg-label-danger");
                    }
                }

                // 4) Meeting vs Task fallback
                const calendarType = ext.calendar;
                if (calendarType === "Meeting") {
                    const colorName = calendarColors[calendarType] || "success";
                    classes.push("bg-label-" + colorName);
                } else if (classes.length === 0 || (classes.length === 1 && classes.includes("fc-event-readonly"))) {
                    // Default for tasks if no status color applied
                    classes.push("bg-label-secondary");
                }

                return classes;
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
            drop: function (info) {
                const eventData = JSON.parse(info.draggedEl.dataset.event);

                // ❗ async’i lifecycle’dan ayır
                setTimeout(() => {
                    createRuntimeSlotAndRefresh(eventData, info.date);
                }, 0);
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
                    ? " - " + end.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })
                    : "";

                // 📌 Örn: "14:00 - 15:00  Deneme Task"
                const timeRange = `${startStr}${endStr}`;

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
                const ext = info.event.extendedProps;
                const assignedToId = ext.toUserId;
                const loggedInUserId = getUserId();
                console.log("🔥 eventDrop fired", info.event.id);
                // Permission Check
                if (assignedToId && assignedToId !== loggedInUserId) {
                    showToast("You don't have permission to move this task.", "error");
                    info.revert();
                    return;
                }

                // Estimated Time Calculation
                applyEstimatedEndTime(info);
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

                const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/MoveRuntimeSlot`;

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
            eventClick: async function (info) {
                const status = info.event.extendedProps.status;
                const type = info.event.extendedProps.type;

                if (status === 'Completed') {
                    showToast("You don't have permission to show detail of completed items.", "warning");
                    return;
                }

                if (type === 'Meeting') {
                    await handleMeetingClick(info.event);
                    return;
                }

                // For tasks, exclusively open the detail modal (time tracking)
                await openTaskDetailModal(info.event);
            },
            eventResize: async function (info) {
                const ext = info.event.extendedProps;
                const assignedToId = ext.toUserId;
                const loggedInUserId = getUserId();

                // Permission Check
                if (assignedToId && assignedToId !== loggedInUserId) {
                    showToast("You don't have permission to resize this task.", "error");
                    info.revert();
                    return;
                }

                const status = ext.status;
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

                const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/MoveRuntimeSlot`;

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
        modifyToggler();




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

        // Close sidebar when clicking on overlay
        if (appOverlay) {
            appOverlay.addEventListener('click', e => {
                document.body.classList.remove('sidebar-open');
                appOverlay.classList.remove('show');
            });
        }

        // Hide left sidebar if the right sidebar is open
        //btnToggleSidebar.addEventListener('click', e => {
        //    if (offcanvasTitle) {
        //        offcanvasTitle.innerHTML = 'Add Event';
        //    }
        //    btnDeleteEvent.classList.add('d-none');
        //    appCalendarSidebar.classList.remove('show');
        //    appOverlay.classList.remove('show');
        //});

        // Jump to date on sidebar(inline) calendar change
        //inlineCalInstance.config.onChange.push(function (date) {
        //  calendar.changeView(calendar.view.type, moment(date[0]).format('YYYY-MM-DD'));
        //  modifyToggler();
        //  appCalendarSidebar.classList.remove('show');
        //  appOverlay.classList.remove('show');
        //});
        // --- CALENDAR FILTERS ---
        $(document).on('click', '#btnApplyCalendarFilters', async function () {
            const assignedTo = $('#filterAssignedTo').val();
            const priorities = $('#filterPriority').val() || [];
            const deadline = $('#filterDeadline').val();
            const userId = getUserId();
            // Correct logic: Keep currentUserId fixed, use assignedFromUserIds for view
            calendarDefaultFilter.currentUserId = assignedTo ? assignedTo : userId;
            calendarDefaultFilter.priorityIds = priorities.length ? priorities.map(Number) : null;
            calendarDefaultFilter.dueDateFilter = deadline || null;

            console.log("Applying Calendar Filters:", calendarDefaultFilter);

            // Close offcanvas
            const offcanvasEl = document.getElementById('offcanvasTaskOverviewFilters');
            const offcanvas = bootstrap.Offcanvas.getInstance(offcanvasEl);
            if (offcanvas) offcanvas.hide();

            // Reload UI
            await loadCalendarSidebar();
            if (typeof calendar !== "undefined") {
                calendar.refetchEvents();
            }
        });

        $(document).on('click', '#btnClearCalendarFilters', async function () {
            // Reset Select2 controls
            $('#filterAssignedTo, #filterStatus, #filterPriority, #filterDeadline').val(null).trigger('change');
            const userId = getUserId();
            // Correct logic: Keep currentUserId fixed, use assignedFromUserIds for view
            calendarDefaultFilter.currentUserId = userId;
            // Reset global filter object (keep currentUserId)
            calendarDefaultFilter.dueDateFilter = null;
            calendarDefaultFilter.priorityIds = null;
            calendarDefaultFilter.assignedFromUserIds = null;

            console.log("Calendar Filters cleared");

            // Reload UI
            await loadCalendarSidebar();
            if (typeof calendar !== "undefined") {
                calendar.refetchEvents();
            }
        });

        // Load Assigned To users when filter offcanvas is opened
        document.getElementById('offcanvasTaskOverviewFilters')?.addEventListener('show.bs.offcanvas', function () {
            loadAssignedToUsers();
            loadTaskStatuses();
            loadTaskPriorities();
            initDeadlineFilter();
        });




    })();
});


// ================================
// 📅 Takvim Eventlerini Yükleme Modülü
// ================================

// --- HELPER FUNCTIONS FOR SEPARATE MODALS ---

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

window.taskDescriptionQuill = null;

window.meetingDescriptionQuill = null;

async function initMeetingDescriptionQuill() {
    const editorContainer = document.querySelector('#meeting-create-modal-description'); // Re-using task container naming convention? No, target the ID in partial.
    // Wait, the partial uses id="meeting-description"
    const container = document.querySelector('#meeting-description');
    if (container && !window.meetingDescriptionQuill) {
        window.meetingDescriptionQuill = new Quill('#meeting-description', {
            bounds: '#meeting-description',
            placeholder: 'Type meeting details...',
            modules: {
                syntax: true,
                toolbar: fullToolbar
            },
            theme: 'snow'
        });
    }
}
async function initTaskDescriptionQuill() {
    const editorContainer = document.querySelector('#task-description');
    if (editorContainer && !window.taskDescriptionQuill) {
        window.taskDescriptionQuill = new Quill('#task-description', {
            bounds: '#task-description',
            placeholder: 'Type something...',
            modules: {
                syntax: true,
                toolbar: fullToolbar
            },
            theme: 'snow'
        });
    }
}







async function initTaskModalForm() {

    await Promise.all([
        //loadGenericDropdown('#ddlCategory', 'Task/GetTaskCategory'), // Loaded on shown.bs.modal with Business Rules
        loadTaskCategories(),
        loadGenericDropdown('#ddlWorkflow', 'Workflow/GetWorkflows', true),
        loadGenericDropdown('#ddlStatus', 'WorkflowCategory/GetWorkflowStatus'),
        loadGenericDropdown('#ddlPriority', 'WorkflowCategory/GetPriorities'),
        loadUsersDropdown('#ddlAssignee')
    ]);

    $('#taskCreateModal .select2').select2({
        dropdownParent: $('#taskCreateModal'),
        width: '100%'
    });

    initTaskDatePickers();
    await initTaskDescriptionQuill();
}

async function initMeetingModalForm() {
    // 1️⃣  LOAD DROPDOWN DATA first
    await Promise.all([
        loadUsersDropdown('#meeting-attendees'),
        loadUsersDropdown('#meeting-owner'),
        loadMeetingCategories()
        //loadGenericDropdown('#meeting-classification', 'WorkflowCategory/GetMeetingClassifications')
    ]);

    // 2️⃣  POPULATE TIME OPTIONS
    fillMeetingTimesNew();

    // 3️⃣ SAFE SELECT2 INIT: Only init if not already initialized
    // This allows re-entrance without breaking Select2 instances
    $('#meetingCreateModal .select2').select2({
        dropdownParent: $('#meetingCreateModal'),
        width: '100%'
    });

    // 4️⃣  DEFAULT VALUES (Create Mode ONLY)
    // IMPORTANT: If we are in Edit Mode, we SKIP this block entirely.
    if (!window.isEditMode) {
        const today = moment().format('YYYY-MM-DD');
        $("#meeting-start-date").val(today);
        $("#meeting-end-date").val(today);

        const fpStart = document.querySelector("#meeting-start-date")?._flatpickr;
        const fpEnd = document.querySelector("#meeting-end-date")?._flatpickr;
        if (fpStart) fpStart.setDate(today);
        if (fpEnd) {
            fpEnd.setDate(today);
            fpEnd.set('minDate', today);
        }

        const nextSlot = getNext30MinSlot();
        $("#meeting-start-time").val(nextSlot).trigger("change.select2");

        const [h, m] = nextSlot.split(':').map(Number);
        let endM = m + 30;
        let endH = h;
        if (endM >= 60) { endH++; endM = 0; }
        const endSlot = (endH < 24) ? `${endH.toString().padStart(2, '0')}:${endM.toString().padStart(2, '0')}` : "23:30";
        $("#meeting-end-time").val(endSlot).trigger("change.select2");

        const meetingOwner = getUserId();
        const $owner = $('#meeting-owner');
        if (meetingOwner) {
            $owner.val(meetingOwner).trigger("change.select2");
        }
    }
    //initMeetingSelect2Once();

    // 5️⃣  PICKERS & QUILL
    initMeetingPickersNew();
    await initMeetingDescriptionQuill();
}

function initMeetingSelect2Once() {
    $('#meetingCreateModal .select2').each(function () {
        if ($(this).hasClass('select2-hidden-accessible')) return;

        $(this).select2({
            dropdownParent: $('#meetingCreateModal'),
            width: '100%'
        });
    });
}

/**
 * Safely sets Select2 value without triggering recursive loops.
 * Triggers 'change.select2' to update UI but avoids generic 'change' if possible.
 */
function safeSetSelect2($el, value) {
    if (!$el.length) return;
    // Only trigger if value is different (optional optimization) or just force set safely
    $el.val(value).trigger('change.select2');
}

async function populateEditMeetingForm(meeting) {
    // Ensure we are in edit mode
    window.isEditMode = true;

    await Promise.all([

        initMeetingModalForm()
        //loadGenericDropdown('#meeting-classification', 'WorkflowCategory/GetMeetingClassifications')
    ]);
    // Init form (Select2s will simply be skipped if already done)
    //await initMeetingModalForm();

    $('#meeting-name').val(meeting.name);

    // Classification
    if (meeting.categoryId) {
        safeSetSelect2($('#meeting-classification'), meeting.categoryId);
    }

    // Owner (Disabled in Edit)
    if (meeting.ownerId) {
        safeSetSelect2($('#meeting-owner'), meeting.ownerId);
        $('#meeting-owner').prop('disabled', true);
    }

    // Description (Quill)
    if (window.meetingDescriptionQuill) {
        window.meetingDescriptionQuill.root.innerHTML = meeting.description || '';
    }

    // Virtual & Location / Link
    $('#meeting-virtual').prop('checked', meeting.isVirtual || false).trigger('change');
    if (meeting.isVirtual) {
        $('#meeting-link').val(meeting.meetingLink || '');
    } else {
        $('#meeting-location').val(meeting.location || '');
    }

    // Attendees
    if (meeting.assignees && meeting.assignees.length > 0) {
        const assigneeIds = meeting.assignees.map(a => a.id);
        safeSetSelect2($('#meeting-attendees'), assigneeIds);
    }

    // Date & Time Handling
    if (meeting.startDate) {
        const start = moment(meeting.startDate);
        const startDate = start.format("YYYY-MM-DD");
        const startTime = start.format("HH:mm");

        $('#meeting-start-date').val(startDate);
        const fpStart = document.querySelector("#meeting-start-date")._flatpickr;
        if (fpStart) fpStart.setDate(startDate);

        safeSetSelect2($('#meeting-start-time'), startTime);
    }

    if (meeting.endDate) {
        const end = moment(meeting.endDate);
        const endDate = end.format("YYYY-MM-DD");
        const endTime = end.format("HH:mm");

        $('#meeting-end-date').val(endDate);
        const fpEnd = document.querySelector("#meeting-end-date")._flatpickr;
        if (fpEnd) fpEnd.setDate(endDate);

        safeSetSelect2($('#meeting-end-time'), endTime);
    }

    // Visibility logic for Delete Button
    const currentUserId = getUserId();
    const isOwner = meeting.ownerId === currentUserId;
    const hasEnded = moment(meeting.endDate, "DD.MM.YYYY")
        .endOf("day")
        .isBefore(moment());

    // meeting.typeId === 2 is checked as per rules
    if (isOwner && !hasEnded && meeting.typeId === 2) {
        $('.btn-delete-meeting').removeClass('d-none');
    } else {
        $('.btn-delete-meeting').addClass('d-none');
    }
}

// Reuse logic but simplified
async function loadGenericDropdown(selector, apiPath, isWorkflow = false) {
    const url = `${API.ppm}/${apiPath}`;
    const res = await fetch(url);
    const result = await res.json();
    const data = Array.isArray(result) ? result : (result.data || []);

    const $el = $(selector);
    $el.empty();
    $el.append('<option value=""></option>');

    data.forEach(item => {
        $el.append(new Option(item.name, item.id));
    });
}
async function loadTaskCategories() {
    const $category = $('#ddlCategory');
    if (!$category.length) return;
    console.log("Loading task categories...");
    const url = `${API.ppm}/Task/GetTaskCategory`;

    try {
        const response = await fetch(url, {
            method: 'GET',
            headers: { 'Content-Type': 'application/json' }
        });

        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        const categories = result.data || result || [];

        // Clear & add empty option for Select2 placeholder
        $category.empty().append('<option></option>');

        categories.forEach(cat => {
            // Business rule: id > 10 olanları listeleme
            if (Number(cat.id) > 10) return;

            const name = cat.name || 'Unknown';
            const option = new Option(name, cat.id, false, false);
            $category.append(option);
        });

        // Select2 refresh

        //$category.trigger('change');

        console.log("Task categories loaded.");
    } catch (err) {
        console.error('Error loading task categories:', err);
    }
}
async function loadUsersDropdown(selector) {
    const url = `${API.legacy.user}/api/PvUser/User/GetUsersByTenantId`;
    const res = await fetch(url);
    const result = await res.json();
    const data = result.data || [];

    const $el = $(selector);
    $el.empty();
    data.forEach(u => {
        const option = new Option(u.fullName, u.id);
        $(option).attr('data-email', u.email || ''); // Store email
        $el.append(option);
    });
}
async function loadMeetingCategories() {
    const $category = $('#meeting-classification');
    if (!$category.length) return;
    console.log("Loading task categories...");
    const url = `${API.ppm}/Task/GetTaskCategory`;

    try {
        const response = await fetch(url, {
            method: 'GET',
            headers: { 'Content-Type': 'application/json' }
        });

        if (!response.ok) throw new Error('API request failed');

        const result = await response.json();
        const categories = result.data || result || [];

        // Clear & add empty option for Select2 placeholder
        $category.empty().append('<option></option>');

        categories.forEach(cat => {
            // Business rule: id > 10 olanları listeleme
            if (Number(cat.id) < 10) return;

            const name = cat.name || 'Unknown';
            const option = new Option(name, cat.id, false, false);
            $category.append(option);
        });

        // Select2 refresh

        //$category.trigger('change');

        console.log("Meeting categories loaded.");
    } catch (err) {
        console.error('Error loading meeting categories:', err);
    }
}

function fillMeetingTimesNew() {
    const $s = $('#meeting-start-time');
    const $e = $('#meeting-end-time');
    $s.empty(); $e.empty();

    for (let h = 0; h < 24; h++) {
        for (let m = 0; m < 60; m += 30) {
            const time = `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`;
            $s.append(new Option(time, time));
            $e.append(new Option(time, time));
        }
    }
}

function initMeetingPickersNew() {
    const today = new Date().toISOString().split("T")[0];
    const $modal = $('#meetingCreateModal');

    if (!document.querySelector("#meeting-start-date")._flatpickr) {
        flatpickr("#meeting-start-date", {
            dateFormat: "Y-m-d",
            minDate: today,
            static: true,
            appendTo: $modal[0]
        });
    }

    if (!document.querySelector("#meeting-end-date")._flatpickr) {
        flatpickr("#meeting-end-date", {
            dateFormat: "Y-m-d",
            minDate: today,
            static: true,
            appendTo: $modal[0]
        });
    }
}


// --- DATE VALIDATION ---
function validateTaskDates() {
    const $start = $("#txt-start-date");
    const $end = $("#txt-end-date");

    // Clear previous errors
    $start.removeClass("is-invalid");
    $end.removeClass("is-invalid");
    $(".date-error-msg").remove();

    const startVal = $start.val();
    const endVal = $end.val();

    if (!startVal || !endVal) return true; // Let the required field check handle empty fields

    const startDate = new Date(startVal);
    const endDate = new Date(endVal);

    if (startDate > endDate) {
        $start.addClass("is-invalid");
        $end.addClass("is-invalid");

        $end.after('<div class="invalid-feedback date-error-msg">End Date cannot be before Start Date.</div>');
        showToast("End Date cannot be before Start Date.", "error");
        return false;
    }

    return true;
}

$(document).on("change", "#txt-start-date, #txt-end-date", function () {
    validateTaskDates();
});

// --- PAYLOAD VALIDATION ---
function validateTaskPayload(payload) {
    const requiredFields = [
        { field: 'name', label: 'Task Name' },
        { field: 'categoryId', label: 'Category' },
        { field: 'statusId', label: 'Status' },
        { field: 'priorityId', label: 'Priority' },
        {
            field: 'assigneeIds',
            label: 'Assignee',
            validate: (val) => val && val.length > 0 && val[0]
        },
        { field: 'startDate', label: 'Start Date' },
        { field: 'endDate', label: 'End Date' },
        { field: 'estimatedHour', label: 'Estimated Hour' },
        { field: 'ownerId', label: 'User Context (Owner)' },
        { field: 'createdBy', label: 'Source (CreatedBy)' }

    ];

    for (const item of requiredFields) {
        const val = payload[item.field];
        let isValid = true;

        if (item.validate) {
            isValid = item.validate(val);
        } else {
            isValid = (val !== null && val !== undefined && val !== '');
        }

        if (!isValid) {
            showToast(`Please fill the mandatory field: <b>${item.label}</b>`, "error");
            return false;
        }
    }

    return true;
}

// --- SAVE HANDLERS ---


$(document).on("click", ".btn-save-task", async function () {
    const $modal = $('#taskCreateModal');
    const mode = $(this).attr("data-mode");
    const id = $(this).attr("data-id");

    // 1) Schedule Validation
    if (!CreateScheduleValidator.validateAll($modal)) {
        // Redirection to Scheduled tab
        const tabEl = document.querySelector('#schedule-tab');
        if (tabEl) {
            bootstrap.Tab.getOrCreateInstance(tabEl).show();
        }
        showToast("Please fix schedule conflicts before saving.", "error");
        return;
    }

    const payload = {
        id: id,
        name: $("#txt-name").val(),
        description: window.taskDescriptionQuill ? window.taskDescriptionQuill.root.innerHTML : '',
        typeId: 1, // Task
        workflowId: $("#ddlWorkflow").val(),
        categoryId: $("#ddlCategory").val(),
        statusId: $("#ddlStatus").val(),
        priorityId: $("#ddlPriority").val(),
        assigneeIds: [$("#ddlAssignee").val()],
        startDate: $("#txt-start-date").val(),
        endDate: $("#txt-end-date").val(),
        estimatedHour: $("#txt-estimated-hour").val(),
        scheduleItems: collectScheduleItems($modal),
        ownerId: userId,
        createdBy: window.getUserName()
        //modifiedBy: window.getUserName()
    };

    if (!validateTaskPayload(payload)) {
        return;
    }

    if (!validateTaskDates()) {
        return;
    }

    await sendSaveRequest(payload, mode, '#taskCreateModal');
});

$(document).on("click", ".btn-save-meeting", async function () {
    const $btn = $(this);
    if ($btn.prop('disabled')) return;

    $btn.prop('disabled', true);

    const mode = $btn.attr("data-mode");
    const id = $btn.attr("data-id");

    const startDate = $("#meeting-start-date").val();
    const endDate = $("#meeting-end-date").val();
    const startTime = $("#meeting-start-time").val();
    const endTime = $("#meeting-end-time").val();

    const payload = {
        id: id,
        name: $("#meeting-name").val(),
        description: window.meetingDescriptionQuill ? window.meetingDescriptionQuill.root.innerHTML : '',
        classificationId: $("#meeting-classification").val() ? $("#meeting-classification").val() : 0,
        ownerId: $("#meeting-owner").val(),
        isVirtual: $("#meeting-virtual").is(":checked"),
        location: $("#meeting-location").val(),
        meetingLink: $("#meeting-link").val(),
        assigneeIds: $("#meeting-attendees").val(),
        startDate: startDate ? `${startDate}T${startTime}:00` : null,
        endDate: endDate ? `${endDate}T${endTime}:00` : null,
        createdBy: window.getUserName()
    };

    if (!payload.name || !payload.startDate || !payload.endDate || !payload.assigneeIds || payload.assigneeIds.length === 0) {
        showToast("Please fill all required fields", "error");
        $btn.prop('disabled', false);
        return;
    }

    try {
        await saveMeeting(payload, mode);
    } finally {
        $btn.prop('disabled', false);
    }
});

$(document).on("click", ".btn-delete-meeting", function () {
    const meetingId = $(".btn-save-meeting").attr("data-id");
    if (!meetingId) return;

    // meetingId'yi confirm butona taşı
    $("#confirmDeleteBtn").data("id", meetingId).data("type", "meeting");

    const modalEl = document.getElementById("deleteConfirmModal");
    if (modalEl) {
        const modalInstance = new bootstrap.Modal(modalEl);
        modalInstance.show();
    }
});

$(document).on("click", "#confirmDeleteBtn", async function () {
    const $btn = $(this);
    const deleteType = $btn.data("type");
    if (deleteType !== "meeting") return;

    const meetingId = $btn.data("id");
    if (!meetingId) return;

    if ($btn.prop("disabled")) return;
    $btn.prop("disabled", true);

    try {
        const payload = { id: meetingId };

        const response = await fetch(`${API.ppm}/Task/DeleteTask`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            showToast("Failed to delete meeting", "error");
            return;
        }

        // Delete confirm modal kapat
        const deleteModalEl = document.getElementById("deleteConfirmModal");
        if (deleteModalEl) {
            const deleteModalInstance = bootstrap.Modal.getInstance(deleteModalEl);
            if (deleteModalInstance) deleteModalInstance.hide();
        }

        // Meeting modal kapat
        const meetingModalEl = document.getElementById("meetingCreateModal");
        if (meetingModalEl) {
            const meetingModalInstance = bootstrap.Modal.getInstance(meetingModalEl);
            if (meetingModalInstance) meetingModalInstance.hide();
        }

        // Refresh alanları
        if (typeof refreshTaskOverview === "function") refreshTaskOverview();
        if (typeof calendar !== "undefined") calendar.refetchEvents();
        if (typeof loadCalendarSidebar === "function") loadCalendarSidebar();

        showToast("Meeting deleted successfully", "success");

    } catch (err) {
        console.error("Delete meeting error:", err);
        showToast("Error while deleting meeting", "error");
    } finally {
        $btn.prop("disabled", false);
        $btn.removeData("id").removeData("type");
    }
});

async function saveMeeting(payload, mode) {
    if (payload.isVirtual && !window.isEditMode) {
        // Construct Google Calendar deep link
        const title = encodeURIComponent(payload.name);
        const details = encodeURIComponent(window.meetingDescriptionQuill ? window.meetingDescriptionQuill.root.innerText : '');

        // Format: 20230526T100000/20230526T110000
        const start = payload.startDate.replace(/[-:]/g, '').split('.')[0];
        const end = payload.endDate.replace(/[-:]/g, '').split('.')[0];
        const gDates = `${start}/${end}`;

        const emails = $('#meeting-attendees option:selected').map(function () {
            return $(this).data('email');
        }).get().filter(e => !!e).join(',');

        const gUrl = `https://calendar.google.com/calendar/render?action=TEMPLATE&text=${title}&dates=${gDates}&details=${details}${emails ? '&add=' + emails : ''}`;

        window.open(gUrl, '_blank');
    }

    await sendSaveMeetingRequest(payload, mode);
}

async function sendSaveMeetingRequest(payload, mode) {
    // TODO: Replace with dedicated Meeting API endpoint
    const url = mode === 'edit'
        ? `${protocol}//${domain}:${port}/services/DitenPPM/Task/upsert-meeting`
        : `${protocol}//${domain}:${port}/services/DitenPPM/Task/upsert-meeting`;

    try {
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await res.json();

        if (!result.errors || result.errors.length === 0) {
            showToast("Success", "success");
            $('#meetingCreateModal').modal("hide");
            if (typeof calendar !== "undefined") calendar.refetchEvents();
            if (typeof loadCalendarSidebar === "function") loadCalendarSidebar();
        } else {
            showToast(result.message || "Error", "error");
        }
    } catch (e) {
        console.error(e);
        showToast("Unexpected error", "error");
    }
}


async function sendSaveRequest(payload, mode, modalId) {
    const url = mode === 'edit'
        ? `${protocol}//${domain}:${port}/services/DitenPPM/Task/UpsertTask`
        : `${protocol}//${domain}:${port}/services/DitenPPM/Task/UpsertTask`;

    try {
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await res.json();

        if (!result.errors || result.errors.length === 0) {
            showToast("Success", "success");
            $(modalId).modal("hide");
            if (typeof calendar !== "undefined") calendar.refetchEvents();
            if (typeof loadCalendarSidebar === "function") loadCalendarSidebar();
        } else {
            showToast(result.message || "Error", "error");
        }
    } catch (e) {
        console.error(e);
        showToast("Unexpected error", "error");
    }
}

// --- READONLY TASK MODAL LOGIC (#taskReadonlyModal) ---
let readonlyScheduleItems = [];

async function openTaskReadonlyModal(task) {
    window.currentTaskId = task.id;
    TaskFormState.currentTask = task;

    // 1) Fill static fields
    $('#readonly-task-name').text(task.name);
    $('#readonly-task-description').html(task.description || '<span class="text-muted italic">No description provided.</span>');
    $('#readonly-task-owner').text(task.ownerName || 'Unknown');
    $('#readonly-task-due-date').text(task.deadlinePretty || (task.endDate ? moment(task.endDate).format("DD.MM.YYYY") : '-'));

    // tags/badges
    $('#readonly-task-status').html(`<span class="badge ${getStatusBadgeColor(task.statusId)}">${task.statusName || 'N/A'}</span>`);
    $('#readonly-task-priority').html(`<span class="badge ${getPriorityBadgeColor(task.priorityId)}">${task.priorityName || 'N/A'}</span>`);

    // assignees
    const $assignees = $('#readonly-task-assignees');
    $assignees.empty();
    if (task.assignees && task.assignees.length > 0) {
        task.assignees.forEach(a => {
            $assignees.append(`<span class="badge bg-label-secondary border me-1">${a.name}</span>`);
        });
    } else {
        $assignees.text('-');
    }

    // 2) Load Schedules
    $("#readonly-scheduleList").empty();
    if (task.scheduleItems && task.scheduleItems.length > 0) {
        task.scheduleItems.forEach(item => addReadonlyScheduleItem(item));
    }

    // 3) Open Modal
    const modalEl = document.getElementById('taskReadonlyModal');
    const modal = new bootstrap.Modal(modalEl);

    // Switch to General tab by default
    const firstTabEl = document.querySelector('button[data-bs-target="#task-readonly-general"]');
    if (firstTabEl) {
        bootstrap.Tab.getInstance(firstTabEl)?.show() || new bootstrap.Tab(firstTabEl).show();
    }

    modal.show();
}

function addReadonlyScheduleItem(data = null) {
    const template = document.getElementById('readonly-schedule-item-template');
    if (!template) return;

    const clone = template.content.cloneNode(true);
    const $item = $(clone.querySelector('.schedule-item'));

    // Init components
    const $start = $item.find('.start-date');
    const $end = $item.find('.end-date');
    const $startTime = $item.find('.start-time');
    const $endTime = $item.find('.end-time');
    const $allDay = $item.find('.all-day-checkbox');

    fillReadonlyTimeDropdown($startTime);
    fillReadonlyTimeDropdown($endTime);

    $start.flatpickr({ dateFormat: "d.m.Y" });
    $end.flatpickr({ dateFormat: "d.m.Y" });
    $startTime.select2({ dropdownParent: $('#taskReadonlyModal'), width: '100%' });
    $endTime.select2({ dropdownParent: $('#taskReadonlyModal'), width: '100%' });

    function fillReadonlyTimeDropdown($el) {
        $el.empty();
        readonlyTimeOptions.forEach(opt => {
            $el.append(new Option(opt.text, opt.id));
        });
    }

    // Event Handlers
    const validationTrigger = () => ReadonlyScheduleValidator.validateAll();

    $item.find(".btnRemoveReadonlyScheduleItem").on("click", function (e) {
        if ($item.hasClass("past-schedule")) {
            e.preventDefault();
            e.stopPropagation();
            return false;
        }
        $item.remove();
        validationTrigger();
    });

    $allDay.on("change", function () {
        if ($(this).is(":checked")) {
            $item.find('.time-section').hide();
        } else {
            $item.find('.time-section').show();
        }
        validationTrigger();
    });

    $start.on("change", validationTrigger);
    $end.on("change", validationTrigger);
    $startTime.on("change", validationTrigger);
    $endTime.on("change", validationTrigger);

    // Pre-fill
    if (data) {
        if (data.startDate) {
            const dateStr = moment(data.startDate).format("DD.MM.YYYY");
            $start[0]._flatpickr?.setDate(dateStr);
        }
        if (data.endDate) {
            const dateStr = moment(data.endDate).format("DD.MM.YYYY");
            $end[0]._flatpickr?.setDate(dateStr);
        }

        if (data.startTime) {
            const t = data.startTime.substring(0, 5);
            $startTime.val(t).trigger('change');
        }
        if (data.endTime) {
            const t = data.endTime.substring(0, 5);
            $endTime.val(t).trigger('change');
        }

        if (data.isAllDay) {
            $allDay.prop('checked', true);
            $item.find('.time-section').hide();
        }

        // Past Schedule Check - Lock if past
        const startMoment = ReadonlyScheduleValidator.getMomentFromItem($item, 'start');
        if (startMoment && startMoment.isBefore(moment())) {
            $item.addClass('past-schedule opacity-75');
            $start.prop('disabled', true).addClass('bg-light');
            $end.prop('disabled', true).addClass('bg-light');
            $startTime.prop('disabled', true);
            $endTime.prop('disabled', true);
            $allDay.prop('disabled', true);

            const $removeBtn = $item.find(".btnRemoveReadonlyScheduleItem");
            $removeBtn.addClass("disabled opacity-50")
                .attr("aria-disabled", "true")
                .css({ pointerEvents: "auto", cursor: "not-allowed" });

            // Bootstrap Tooltip Init
            const btnEl = $removeBtn[0];
            const existingTooltip = bootstrap.Tooltip.getInstance(btnEl);
            if (existingTooltip) existingTooltip.dispose();

            new bootstrap.Tooltip(btnEl, {
                title: 'Past schedule cannot be modified or removed',
                placement: 'top',
                container: '#taskReadonlyModal',
                trigger: 'hover focus'
            });
        }
    }

    $("#readonly-scheduleList").append($item);
    validationTrigger();
}

/** --- Readonly Schedule Validator Logic --- **/
const ReadonlyScheduleValidator = {
    validateAll: function () {
        let hasError = false;
        const items = $("#readonly-scheduleList .schedule-item");
        const scheduleData = [];

        // 1. Individual Item Validation (Date/Time)
        items.each((idx, el) => {
            const $item = $(el);
            const errors = this.validateItem($item);
            this.showErrors($item, errors);
            if (errors.length > 0) hasError = true;

            // Collect valid items for overlap check
            if (errors.length === 0) {
                scheduleData.push({
                    $el: $item,
                    start: this.getMomentFromItem($item, 'start'),
                    end: this.getMomentFromItem($item, 'end')
                });
            }
        });

        // 2. Overlap Validation
        if (scheduleData.length > 1) {
            for (let i = 0; i < scheduleData.length; i++) {
                for (let j = i + 1; j < scheduleData.length; j++) {
                    const a = scheduleData[i];
                    const b = scheduleData[j];

                    if (a.start && a.end && b.start && b.end) {
                        if (a.start.isBefore(b.end) && b.start.isBefore(a.end)) {
                            hasError = true;
                            this.addOverlapError(a.$el, b.start, b.end);
                            this.addOverlapError(b.$el, a.start, a.end);
                        }
                    }
                }
            }
        }

        this.toggleSaveButton(hasError);
    },

    validateItem: function ($item) {
        const errors = [];
        const startVal = $item.find(".start-date").val();
        const endVal = $item.find(".end-date").val();
        const isAllDay = $item.find(".all-day-checkbox").is(":checked");

        if (!startVal || !endVal) return errors;

        const mStart = moment(startVal, "DD.MM.YYYY");
        const mEnd = moment(endVal, "DD.MM.YYYY");

        if (mStart.isAfter(mEnd, 'day')) {
            errors.push("Start date cannot be later than end date");
        } else if (mStart.isSame(mEnd, 'day') && !isAllDay) {
            const startTime = $item.find(".start-time").val();
            const endTime = $item.find(".end-time").val();
            if (startTime && endTime) {
                const [sh, sm] = startTime.split(':').map(Number);
                const [eh, em] = endTime.split(':').map(Number);
                const sTotal = sh * 60 + sm;
                const eTotal = eh * 60 + em;
                if (sTotal >= eTotal) {
                    errors.push("Start time must be earlier than end time for same day");
                }
            }
        }
        return errors;
    },

    showErrors: function ($item, errors) {
        let $alertWrap = $item.find(".alert-wrapper");

        if (errors.length === 0) {
            $alertWrap.remove();
            $item.removeClass('border-danger');
            return;
        }

        if ($alertWrap.length === 0) {
            const html = `
                <div class="col-12 mt-3 alert-wrapper">
                    <div class="alert alert-danger alert-dismissible fade show mb-0" role="alert">
                        <div class="d-flex align-items-center mb-1">
                            <i class="bx bx-error me-2 fs-5 text-danger"></i>
                            <h6 class="alert-heading mb-0 fw-bold">Schedule Error</h6>
                        </div>
                        <div class="item-errors small opacity-90 ms-4 fw-bold"></div>
                        
                    </div>
                </div>`;
            $item.find('.row').append(html);
            $alertWrap = $item.find(".alert-wrapper");
        }

        $item.find(".item-errors").html(errors.join("<br>"));
        $item.addClass('border-danger').removeClass('border-secondary');
    },

    addOverlapError: function ($item, otherStart, otherEnd) {
        const msg = `Schedule overlaps with another slot (${otherStart.format('DD.MM HH:mm')} - ${otherEnd.format('DD.MM HH:mm')})`;
        let $errCont = $item.find(".item-errors");

        if ($errCont.length === 0) {
            this.showErrors($item, [msg]);
            return;
        }

        if ($errCont.text().indexOf("overlaps") === -1) {
            const current = $errCont.html();
            $errCont.html(current + (current ? "<br>" : "") + msg);
            $item.addClass('border-danger').removeClass('border-secondary');
        }
    },

    getMomentFromItem: function ($item, type) {
        const dateVal = $item.find(`.${type}-date`).val();
        const timeVal = $item.find(`.${type}-time`).val();
        const isAllDay = $item.find(".all-day-checkbox").is(":checked");

        if (!dateVal) return null;
        let m = moment(dateVal, "DD.MM.YYYY");
        if (isAllDay) {
            return type === 'start' ? m.startOf('day') : m.endOf('day');
        }
        if (!timeVal) return null;
        const [h, min] = timeVal.split(':');
        return m.hour(h).minute(min).second(0);
    },

    toggleSaveButton: function (hasError) {
        $("#btnReadonlySaveTaskSchedule").prop('disabled', hasError);
    }
};

// Global button listeners for Readonly Modal
$(document).off('click', '#btnReadonlyAddScheduleItem').on('click', '#btnReadonlyAddScheduleItem', () => addReadonlyScheduleItem());

$(document).off('click', '#btnReadonlySaveTaskSchedule').on('click', '#btnReadonlySaveTaskSchedule', async function () {
    const $btn = $(this);
    if (!TaskFormState.currentTask) return;
    $btn.prop('disabled', true);

    const scheduleItems = [];
    $("#readonly-scheduleList .schedule-item").each(function () {
        const startDate = $(this).find(".start-date").val();
        const endDate = $(this).find(".end-date").val();
        const startTime = $(this).find(".start-time").val();
        const endTime = $(this).find(".end-time").val();
        const isAllDay = $(this).find(".all-day-checkbox").is(":checked");

        let start = null, end = null;
        if (startDate) {
            const d = moment(startDate, "DD.MM.YYYY").format("YYYY-MM-DD");
            start = isAllDay ? `${d}T00:00:00` : (startTime ? `${d}T${startTime}:00` : null);
        }
        if (endDate) {
            const d = moment(endDate, "DD.MM.YYYY").format("YYYY-MM-DD");
            end = isAllDay ? `${d}T23:59:59` : (endTime ? `${d}T${endTime}:00` : null);
        }

        scheduleItems.push({
            startDate: startDate ? moment(startDate, "DD.MM.YYYY").format("YYYY-MM-DD") : null,
            endDate: endDate ? moment(endDate, "DD.MM.YYYY").format("YYYY-MM-DD") : null,
            startTime,
            endTime,
            start,
            end,
            isAllDay
        });
    });

    const task = TaskFormState.currentTask;
    const payload = {
        id: task.id,
        name: task.name,
        description: task.description,
        typeId: task.typeId || 1,
        categoryId: task.categoryId || task.categId,
        workflowId: task.workflowId,
        statusId: task.statusId,
        priorityId: task.priorityId,
        assigneeIds: task.assigneeIds || [],
        startDate: task.startDate ? task.startDate.split('T')[0] : null,
        endDate: task.endDate ? task.endDate.split('T')[0] : null,
        estimatedHour: task.estimatedHour,
        scheduleItems: scheduleItems,
        ownerId: task.ownerId,
        modifiedBy: window.getUserName()
    };

    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Task/UpdateTaskOrMeeting`;
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });
        const result = await res.json();
        if (!result.errors || result.errors.length === 0) {
            showToast("Schedule updated successfully", "success");
            bootstrap.Modal.getInstance(document.getElementById('taskReadonlyModal'))?.hide();
            if (typeof calendar !== "undefined") calendar.refetchEvents();
        } else {
            showToast(result.message || "Error updating schedule", "error");
        }
    } catch (err) {
        console.error(err);
        showToast("Unexpected error", "error");
    } finally {
        $btn.prop('disabled', false);
    }
});

function getStatusBadgeColor(id) {
    switch (id) {
        case 1: return "bg-label-secondary"; // Pending
        case 2: return "bg-label-info";      // In Progress
        case 3: return "bg-label-success";   // Completed
        case 4: return "bg-label-danger";    // Cancelled
        default: return "bg-label-primary";
    }
}

/* --- TaskReadonlyModal UI Logic --- */
const TaskReadonlyModalUI = {
    init: function () {
        this.initOverdueObserver();
        this.initViewToggle();
    },
    initOverdueObserver: function () {
        const dateTarget = document.getElementById('readonly-task-due-date');
        if (!dateTarget) return;

        const dateObs = new MutationObserver(() => {
            const dateStr = $('#readonly-task-due-date').text().trim();
            if (dateStr && dateStr !== '-' && dateStr !== '') {
                let dueDate = moment(dateStr, "DD.MM.YYYY");
                if (!dueDate.isValid()) dueDate = moment(dateStr);

                if (dueDate.isValid() && dueDate.isBefore(moment(), 'day')) {
                    $('#jira-overdue-accent').show();
                    $('#readonly-task-due-date').addClass('text-danger').attr('title', 'Overdue');
                } else {
                    $('#jira-overdue-accent').hide();
                    $('#readonly-task-due-date').removeClass('text-danger').removeAttr('title');
                }
            }
        });

        dateObs.observe(dateTarget, { childList: true, characterData: true, subtree: true });
    },
    initViewToggle: function () {
        $(document).on('click', '.view-toggle-btn', function () {
            const $btn = $(this);
            const view = $btn.data('view');

            $btn.siblings('.view-toggle-btn').removeClass('active');
            $btn.addClass('active');

            const list = $('#readonly-scheduleList');
            if (view === 'list') {
                list.removeClass('jira-timeline').addClass('jira-list');
            } else {
                list.removeClass('jira-list').addClass('jira-timeline');
            }
        });
    }
};

$(function () {
    TaskReadonlyModalUI.init();
});








