'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const port2 = protocol === 'https:' ? '5055' : '5050';
const userName = window.getUserName();
function showProjectLoader(text = "Loading project...") {
    const loader = document.getElementById("projectPageLoader");
    if (!loader) return;

    loader.querySelector(".fw-semibold").textContent = text;
    loader.classList.remove("hidden");
}

function hideProjectLoader() {
    const loader = document.getElementById("projectPageLoader");
    if (!loader) return;

    loader.classList.add("hidden");
}


// --- MAIN FORM PICKERS ---
const chkNoEndDate = document.getElementById("checkNoEndDate");
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
const endInput = document.querySelector("#dtEndDate");

chkNoEndDate.addEventListener("change", function () {
    endInput.classList.toggle("bg-light", this.checked);
    endInput.classList.toggle("text-muted", this.checked);
});
chkNoEndDate.addEventListener("change", function () {

    if (this.checked) {
        // 🔒 End Date disable
        endPicker.clear();
        endPicker.set("disable", [() => true]); // tüm günleri disable et

        // Start tarafındaki maxDate kuralını kaldır
        startPicker.set("maxDate", null);
    }
    else {
        // 🔓 End Date tekrar aktif
        endPicker.set("disable", []);

        // Eğer start seçiliyse, tekrar minDate ata
        const startVal = startPicker.selectedDates[0];
        if (startVal) {
            endPicker.set("minDate", startVal);
        }
    }
});

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


async function CreateProject() {
    populateSelect('ddl-status', {
        apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
        placeholder: 'Select status',
        valueKey: 'id',
        textKey: 'name',
        filter: s => s.id !== 3

    });

    populateSelect('ddl-priority', {
        apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetPriorities`,
        placeholder: 'Select priority',
        valueKey: 'id',
        textKey: 'name',


    });

    populateSelect('ddl-record-type', {
        apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetRecordTypes`,
        placeholder: 'Select record type',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

    });

    populateSelect('ddl-manager', {
        apiUrl: `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`,
        placeholder: 'Select manager',
        valueKey: 'id',
        textKey: 'fullName',
        autoSelectIfSingle: true

    });

    bindDependentSelect('ddl-record-type', 'ddl-category', {
        apiUrlBuilder: (recordTypeId) => `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowCategoriesByRecordTypeId/${recordTypeId}`,
        placeholder: 'Select category',
    });
    loadWorkflowCode();
    setProjectHeader({ mode: "create" });
    setProjectPageMode({ mode: "create" });

}

async function loadWorkflowCode() {
    try {
        const url = `${protocol}//${domain}:${port}/services/DitenPPM/Workflow/GenerateWorkflowId`;


        const res = await fetch(url);
        const result = await res.json();

        if (result?.data) {
            $("#txt-code").val(result.data);
        }
    } catch (err) {
        console.error("Workflow ID üretilemedi", err);
    }
}


//-------------------------- TEAMS-----------------//
let dummyData = [];
let dt_workflow_team_table;
let dt_workflow_team;
document.querySelector('button[data-bs-target="#teamsForm"]')
    .addEventListener('shown.bs.tab', function () {

        $('.workflow-team-table').DataTable().destroy();
        const categoryId = document.getElementById('ddl-category').value;
        populateSelect('add-team-member', {
            apiUrl: `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`,
            placeholder: 'Select team member',
            valueKey: 'id',
            textKey: 'fullName',
            autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

        });

        if (categoryId) {
            populateSelect('ddlRoles', {
                apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetTeamRolesByWorkflowCategoryId/${categoryId}`,
                placeholder: 'Select role',
                valueKey: 'id',
                textKey: 'name',
                autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

            }); populateSelect('ddlRoles', {
                apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetTeamRolesByWorkflowCategoryId/${categoryId}`,
                placeholder: 'Select role',
                valueKey: 'id',
                textKey: 'name',
                autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

            });
        }
        



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

document.getElementById('btnCancelGeneral').addEventListener('click', function () {
    window.location.href = `/ppm/workflow-overview`;
});
// 1️⃣ Edit butonuna tıklama
$(document).on('click', '.edit-team-record', function () {
    const id = $(this).data('id'); // satır ID'si

    const member = dummyData.find(m => m.id === id);
    const categoryId = document.getElementById('ddl-category').value || "";

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
        url: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetTeamRolesByWorkflowCategoryId/${categoryId}`,
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

function initTeamDataTable(placeholderText, lanData) {

    dt_workflow_team_table = document.querySelector('.workflow-team-table');
    if (!dt_workflow_team_table) return;

    if (dt_workflow_team_table) {



        dt_workflow_team = new DataTable(dt_workflow_team_table, {
            data: dummyData,
            //ajax: {
            //    url: `${protocol}//${domain}:${port}/services/PvSurvey/Survey/GetSurveys`,
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

//-------------------------- TEAMS-----------------//
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

function getUserTotalHours(userId) {

    if (!userId) return 0;

    // Bu kullanıcıya atanmış tüm task ve subtasklar
    //const userTasks = dummyTaskData.filter(t =>
    //    Array.isArray(t.assignee) && !t.parentTaskId &&
    //    t.assignee.some(a => a.id === userId)
    //);
    const totalHours = 0;
    // estimatedHour toplama
    //const totalHours = userTasks.reduce((sum, t) => {
    //    return sum + (Number(t.estimatedHour) || 0);
    //}, 0);

    return totalHours;
}
function getUserTaskStats(userId) {

    // Bu kullanıcıya atanmış tüm task ve subtasklar
    //const userTasks = dummyTaskData.filter(t =>
    //    Array.isArray(t.assignee) &&
    //    t.assignee.some(a => a.id === userId)
    //);

    //const total = userTasks.length;

    //const completed = userTasks.filter(t => Number(t.statusId) === 3).length;
    const completed = 0;
    const total = 0;
    return { total, completed };
}

async function loadWorkflow(workflowId) {
    try {
        const response = await fetch(`${protocol}//${domain}:${port}/services/DitenPPM/Workflow/GetWorkflowById/${workflowId}`);
        if (!response.ok) throw new Error("API error");
        const result = await response.json();
        const data = result?.data;
        dummyData = data?.workFlowTeams || [];

        populateSelect('ddl-status', {
            apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowStatus`,
            placeholder: 'Select status',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: data?.workflowStatusId || 0  // Tek kayıt varsa otomatik seçer

        });

        populateSelect('ddl-priority', {
            apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetPriorities`,
            placeholder: 'Select priority',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: data?.priorityId || 0  // Tek kayıt varsa otomatik seçer

        });
        populateSelect('ddl-record-type', {
            apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetRecordTypes`,
            placeholder: 'Select record type',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: data?.recordTypeId || 0  // Tek kayıt varsa otomatik seçer

        });
        bindDependentSelect('ddl-record-type', 'ddl-category', {
            apiUrlBuilder: (recordTypeId) =>
                `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowCategoriesByRecordTypeId/${recordTypeId}`,
            placeholder: 'Select category',
            selectedChildValue: data?.workflowCategoryId || '' // 👈 EDIT DEĞERİ
        });
        populateSelect('ddl-manager', {
            apiUrl: `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`,
            placeholder: 'Select manager',
            valueKey: 'id',
            textKey: 'fullName',
            selectedValue: data?.userId || ''  // Tek kayıt varsa otomatik 
        });


        $('#txt-name').val(data?.name || "");
        $('#txt-code').val(data?.idCode || "");
        $('#txt-description').val(data?.description || "");
        const startLocal = toLocalDateOnly(data?.startDate);
        const endLocal = toLocalDateOnly(data?.endDate);

        document.getElementById("dtStartDate")._flatpickr.setDate(startLocal, true);
        document.getElementById("dtEndDate")._flatpickr.setDate(endLocal, true);

        setNoEndDateFromApi(data?.isOnGoing);
        setProjectHeader({
            mode: "edit",
            workflowName: data?.name
        });
        setProjectPageMode({ mode: "edit" });

    } catch (err) {
        console.error(err);
        dummyData = []; // boş template
    }
}

function setProjectPageMode({ mode }) {

    const btn = document.getElementById("btnCreate");

    if (!btn) return;

    if (mode === "edit") {
        btn.textContent = "Update";
        //btn.classList.remove("btn-label-primary");
        //btn.classList.add("btn-primary"); // isteğe bağlı daha güçlü vurgu
    }
    else {
        btn.textContent = "Create";
        //btn.classList.remove("btn-primary");
        //btn.classList.add("btn-label-primary");
    }
}

function setProjectHeader({ mode, workflowName }) {

    const titleEl = document.getElementById("pageTitle");
    const breadcrumbEl = document.getElementById("breadcrumbActive");

    if (mode === "edit") {
        titleEl.textContent = "Edit Project";
        breadcrumbEl.textContent = workflowName || "Project";
    }
    else {
        titleEl.textContent = "New Project";
        breadcrumbEl.textContent = "New Projects";
    }
}

function setNoEndDateFromApi(isNoEndDate) {

    const chkNoEndDate = document.getElementById("checkNoEndDate");

    // 1️⃣ Checkbox state
    chkNoEndDate.checked = !!isNoEndDate;

    // 2️⃣ Senin yazdığın change logic çalışsın diye
    chkNoEndDate.dispatchEvent(new Event("change"));
}

function toLocalDateOnly(utcString) {
    const d = new Date(utcString);

    const pad = n => String(n).padStart(2, "0");

    return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()}`;
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
            ddlRecordType: {
                validators: {
                    notEmpty: {
                        message: 'Record type is required'
                    }
                }
            },
            ddlCategory: {
                validators: {
                    notEmpty: {
                        message: 'Category is required'
                    }
                }
            },
            ddlManager: {
                validators: {
                    notEmpty: {
                        message: 'Manager is required'
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
                    const recordTypeId = document.getElementById('ddl-record-type').value || 0;
                    const categoryId = document.getElementById('ddl-category').value || '';
                    const WorkflowStatusId = document.getElementById('ddl-status').value;
                    const WorkflowPriorityId = document.getElementById('ddl-priority').value;
                    const userName = window.getUserName();
                    const ownerId = document.getElementById('ddl-manager').value;
                    const startDateValue = document.getElementById("dtStartDate")._flatpickr.selectedDates[0];
                    const endDateValue = document.getElementById("dtEndDate")._flatpickr.selectedDates[0];
                    const startISO = startDateValue ? startDateValue.toISOString() : null;
                    const isChecked = $('#checkNoEndDate').is(':checked');
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
                            workFlowTasks: [],
                            ownerId: ownerId,
                            isOnGoing: isChecked

                        };
                        fetch(`${protocol}//${domain}:${port}/services/DitenPPM/Workflow/CreateWorkflow`, {
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
                            modifiedBy: userName,
                            workFlowTasks: [],
                            ownerId: ownerId,
                            isOnGoing: isChecked

                        };
                        fetch(`${protocol}//${domain}:${port}/services/DitenPPM/Workflow/UpdateWorkflow`, {
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


document.addEventListener('DOMContentLoaded', async function () {
    showProjectLoader("Preparing project...");
    //const recordType = urlParams.get('recordType') || '';
    //const category = urlParams.get('category') || '';
    const urlParams = new URLSearchParams(window.location.search);
    const pageMode = urlParams.get('mode') || 'create'; 
    const workflowId = window.location.pathname.split('/')[2];
    if (pageMode === 'view') {
        document
            .getElementById('projectCreationPage')
            .classList.add('view-mode');
    }
    try {

        if (workflowId === 'blank') {
            await CreateProject();
        } else {
            await loadWorkflow(workflowId);
        }

        initializeCharacterCounter('txt-description', 2000);
        initializeCharacterCounter('txt-name', 250);
        initializeFormValidation();

    } catch (err) {
        console.error(err);
        showToast("Project could not be loaded", "error");
    }
    finally {
        hideProjectLoader(); // 🔥 her şey bitince
    }





    initializeCharacterCounter('txt-description', 2000);
    initializeCharacterCounter('txt-name', 250);
    initializeFormValidation();

});