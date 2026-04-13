'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const userName = window.getUserName();
let dt_workflow_table;
let dt_workflow;

document.addEventListener('DOMContentLoaded', function () {

    const lang = localStorage.getItem('language') || 'en';
    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["Search"] || "Search";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Language file could not be loaded:', error);
            initDataTable("Search", data); // fallback
        });

    populateSelect('add-record-type', {
        apiUrl: `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetRecordTypes`,
        placeholder: 'Select record type',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

    });

    bindDependentSelect('add-record-type', 'add-category', {
        apiUrlBuilder: (recordTypeId) => `${protocol}//${domain}:${port}/services/DitenPPM/WorkflowCategory/GetWorkflowCategoriesByRecordTypeId/${recordTypeId}`,
        placeholder: 'Select category',
    });

    bindDeleteRecordEvent();
    initializeFormValidation();
});

function initDataTable(placeholderText, lanData) {
    const userId = window.getUserId();
    dt_workflow_table = document.querySelector('.workflow-table');
    if (!dt_workflow_table) return;

    if (dt_workflow_table) {
        dt_workflow = new DataTable(dt_workflow_table, {
            ajax: {
                url: `${protocol}//${domain}:${port}/services/DitenPPM/Workflow/GetWorkflows?currentUserId=${userId}`,
                method: 'GET',
                dataSrc: 'data',
                error: function (jqxhr, textStatus, errorThrown) {
                    // Bu callback sadece DataTable özelinde hata olursa çalışır
                    console.error("DataTable Error:", jqxhr.status, errorThrown);
                    if (jqxhr.status !== 200) {
                        window.location.href = '/pages-misc-error.html?code=' + jqxhr.status; // Hata kodu ile yönlendirme
                    }
                }
            },
            columns: [
                { data: 'id' },
                { data: 'name' },
                { data: 'recordTypeName' },
                { data: 'priorityName' },
                { data: 'progress' },
                { data: 'userName' },
                {
                    data: null,
                    render: (data, type, row) => `${row.completedTask}/${row.totalTask}`
                },
                {
                    data: null,
                },
                { data: 'workflowStatusName' },
                { data: null }
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
                    targets: 7,
                    responsivePriority: 3,
                    className: 'dt-col-dates',
                    render: (data, type, row) => {

                        const startDate = row.startDate ? moment(row.startDate) : null;
                        const endDate = row.endDate ? moment(row.endDate) : null;

                        const start = startDate ? startDate.format('DD MMM YY') : '-';
                        const end = endDate ? endDate.format('DD MMM YY') : '-';

                        const isOnGoing =
                            row.isOnGoing === true || row.isOnGoing === 'true' ||
                            row.IsOnGoing === true || row.IsOnGoing === 'true';
                        // 🔥 On Going → Badge
                        const endLine = isOnGoing
                            ? `<span class="badge bg-label-warning">On Going</span>`
                            : `<small><strong class="text-danger">E:</strong> ${end}</small>`;

                        return `
            <div class="date-cell d-flex flex-column gap-1">
                <small><strong class="text-primary">S:</strong> ${start}</small>
                ${endLine}
            </div>
        `;
                    }
                },
                {
                    targets: 1,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        const projectId = full.id;
                        const name = full['name'];
                        const description = full['description'];
                        // Creates full output for survey name and description
                        //                   <small class="text-break  survey-desc">${description}</small>

                        return `
            <div class="d-flex justify-content-start align-items-center">
                <div class="d-flex flex-column">
                    <span
                        class="fw-medium text-nowrap project-name"
                        role="link"
                        tabindex="0"
                        data-id="${projectId}"
                        data-name="${name}"
                        aria-label="Open project ${name}"
                    >
                        ${name}
                    </span>
                </div>
            </div>
        `;
                    }
                },
                {
                    targets: 2,
                    responsivePriority: 4,
                    render: (data) =>
                        `<span class="badge bg-label-primary">${data}</span>`
                },
                {
                    targets: 3,
                    responsivePriority: 5,
                    render: function (data, type, full, meta) {
                        let color = 'secondary';
                        const priority = full['priorityId'];
                        if (priority === 3) color = 'danger';
                        else if (priority === 2) color = 'warning';
                        else if (priority === 1) color = 'success';
                        return `<span class="badge bg-label-${color}">${data}</span>`;
                    }
                },
                {
                    targets: 4,
                    responsivePriority: 6,
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
                    targets: 8,
                    responsivePriority: 7,
                    render: (data, type, full) => {
                        let color = 'secondary';
                        const workflowStatusId = full['workflowStatusId'];

                        if (workflowStatusId === 3) color = 'success';
                        else if (workflowStatusId === 2) color = 'info';
                        else if (workflowStatusId === 1) color = 'warning';
                        return `<span class="badge bg-label-${color}">${data}</span>`;
                    }
                },
                {
                    targets: -1,
                    responsivePriority: 9,
                    title: 'Actions',
                    orderable: false,
                    
                    render: function (data, type, full, meta) {
                        let dropdownItems = '';

                            dropdownItems += `
            <a href="javascript:void(0);" class="dropdown-item status-button view-record d-flex alig-items-center" data-id="${full.id}" data-action="view">
    <i class="icon-sm bx bx-show me-2"></i> View
</a>
            <a href="javascript:void(0);" class="dropdown-item status-button edit-record d-flex alig-items-center" data-id="${full.id}" data-action="edit">
    <i class="icon-sm bx bx-edit-alt me-2"></i> Edit
</a>
            <a href="javascript:void(0);" class="dropdown-item status-button open-record d-flex alig-items-center" data-id="${full.id}" data-action="open">
                <i class="icon-sm bx bx-door-open me-2"></i> Open
            </a>
            <a href="javascript:void(0);" class="dropdown-item status-button dublicate-record d-flex alig-items-center" data-id="${full.id}" data-action="dublicate">
                <i class="icon-sm bx bx-copy me-2"></i> Duplicate
            </a>
             <a href="javascript:void(0);" class="dropdown-item status-button archive-record d-flex alig-items-center " data-id="${full.id}" data-action="archive">
                <i class="icon-sm bx bx-archive me-2"></i> Archive
            </a>
            <a href="javascript:void(0);" class="dropdown-item status-button text-danger delete-record d-flex alig-items-center" data-id="${full.id}" data-action="delete">
                <i class="icon-sm bx bx-trash me-2"></i> Delete
            </a>
            `;
                        

                        // Dropdown HTML
                        const dropdown = `
        <div class="btn-group">
            <button class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown">
                <i class="bx bx-dots-vertical-rounded"></i>
            </button>
            <div class="dropdown-menu dropdown-menu-end">
                ${dropdownItems}
            </div>
        </div>`;

                        return `
        <div class="d-flex justify-content-sm-start align-items-sm-center">
            ${dropdown}
        </div>`;
                    }

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
                                //{
                                //    extend: 'collection',
                                //    className: 'btn btn-label-secondary dropdown-toggle',
                                //    text: '<span class="d-flex align-items-center gap-2"><i class="icon-base bx bx-export icon-sm"></i> <span class="d-none d-sm-inline-block">Export</span></span>',
                                //    buttons: [
                                //        {
                                //            extend: 'print',
                                //            text: `<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-2"></i>Print</span>`,
                                //            className: 'dropdown-item',
                                //            exportOptions: {
                                //                columns: [3, 4, 5, 6, 7],
                                //                format: {
                                //                    body: function (inner, coldex, rowdex) {
                                //                        if (inner.length <= 0) return inner;
                                //                        const el = new DOMParser().parseFromString(inner, 'text/html').body.childNodes;
                                //                        let result = '';
                                //                        el.forEach(item => {
                                //                            if (item.classList && item.classList.contains('user-name')) {
                                //                                result += item.lastChild.firstChild.textContent;
                                //                            } else {
                                //                                result += item.textContent || item.innerText || '';
                                //                            }
                                //                        });
                                //                        return result;
                                //                    }
                                //                }
                                //            },
                                //            customize: function (win) {
                                //                win.document.body.style.color = config.colors.headingColor;
                                //                win.document.body.style.borderColor = config.colors.borderColor;
                                //                win.document.body.style.backgroundColor = config.colors.bodyBg;
                                //                const table = win.document.body.querySelector('table');
                                //                table.classList.add('compact');
                                //                table.style.color = 'inherit';
                                //                table.style.borderColor = 'inherit';
                                //                table.style.backgroundColor = 'inherit';
                                //            }
                                //        },
                                //        {
                                //            extend: 'csv',
                                //            text: `<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-2"></i>Csv</span>`,
                                //            className: 'dropdown-item',
                                //            exportOptions: {
                                //                columns: [3, 4, 5, 6, 7],
                                //                format: {
                                //                    body: function (inner, coldex, rowdex) {
                                //                        if (inner.length <= 0) return inner;
                                //                        const el = new DOMParser().parseFromString(inner, 'text/html').body.childNodes;
                                //                        let result = '';
                                //                        el.forEach(item => {
                                //                            if (item.classList && item.classList.contains('user-name')) {
                                //                                result += item.lastChild.firstChild.textContent;
                                //                            } else {
                                //                                result += item.textContent || item.innerText || '';
                                //                            }
                                //                        });
                                //                        return result;
                                //                    }
                                //                }
                                //            }
                                //        },
                                //        {
                                //            extend: 'excel',
                                //            text: `<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-export me-2"></i>Excel</span>`,
                                //            className: 'dropdown-item',
                                //            exportOptions: {
                                //                columns: [3, 4, 5, 6, 7],
                                //                format: {
                                //                    body: function (inner, coldex, rowdex) {
                                //                        if (inner.length <= 0) return inner;
                                //                        const el = new DOMParser().parseFromString(inner, 'text/html').body.childNodes;
                                //                        let result = '';
                                //                        el.forEach(item => {
                                //                            if (item.classList && item.classList.contains('user-name')) {
                                //                                result += item.lastChild.firstChild.textContent;
                                //                            } else {
                                //                                result += item.textContent || item.innerText || '';
                                //                            }
                                //                        });
                                //                        return result;
                                //                    }
                                //                }
                                //            }
                                //        },
                                //        {
                                //            extend: 'pdf',
                                //            text: `<span class="d-flex align-items-center"><i class="icon-base bx bxs-file-pdf me-2"></i>Pdf</span>`,
                                //            className: 'dropdown-item',
                                //            exportOptions: {
                                //                columns: [3, 4, 5, 6, 7],
                                //                format: {
                                //                    body: function (inner, coldex, rowdex) {
                                //                        if (inner.length <= 0) return inner;
                                //                        const el = new DOMParser().parseFromString(inner, 'text/html').body.childNodes;
                                //                        let result = '';
                                //                        el.forEach(item => {
                                //                            if (item.classList && item.classList.contains('user-name')) {
                                //                                result += item.lastChild.firstChild.textContent;
                                //                            } else {
                                //                                result += item.textContent || item.innerText || '';
                                //                            }
                                //                        });
                                //                        return result;
                                //                    }
                                //                }
                                //            }
                                //        },
                                //        {
                                //            extend: 'copy',
                                //            text: `<i class="icon-base bx bx-copy me-1"></i>Copy`,
                                //            className: 'dropdown-item',
                                //            exportOptions: {
                                //                columns: [3, 4, 5, 6, 7],
                                //                format: {
                                //                    body: function (inner, coldex, rowdex) {
                                //                        if (inner.length <= 0) return inner;
                                //                        const el = new DOMParser().parseFromString(inner, 'text/html').body.childNodes;
                                //                        let result = '';
                                //                        el.forEach(item => {
                                //                            if (item.classList && item.classList.contains('user-name')) {
                                //                                result += item.lastChild.firstChild.textContent;
                                //                            } else {
                                //                                result += item.textContent || item.innerText || '';
                                //                            }
                                //                        });
                                //                        return result;
                                //                    }
                                //                }
                                //            }
                                //        }
                                //    ]
                                //},
                                {
                                    text: '<i class="icon-base bx bx-export"></i>',
                                    className: 'download-template btn btn-icon btn-label-secondary'
                                },
                                {
                                    text: '<i class="icon-base bx bx-import"></i>',
                                    className: 'toggle-assigned btn btn-icon btn-label-primary'
                                },
                                {
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">New Project</span>',
                                    className: 'add-new btn btn-primary',
                                    action: function () {
                                        window.location.href = '/ppm/blank/project-creation';
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
                searchPlaceholder: 'Search User',
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
                modifyDataTableLayout();
                // 🔵 Dashboard kartlarını güncelle
                const json = dt_workflow.ajax.json();
                updateWorkflowDashboardCards(json.data);
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
$(document).on('click', '.project-name', function () {
    const projectId = $(this).data('id');
    const projectName = $(this).data('name');
    window.location.href = `/ppm/${projectId}/workstream?workstreamName=${projectName}`;
});

// Keyboard (Enter)
$(document).on('keydown', '.project-name', function (e) {
    if (e.key === 'Enter') {
        e.preventDefault();
        const projectId = $(this).data('id');
        const projectName = $(this).data('name');
        window.location.href = `/ppm/${projectId}/workstream?workstreamName=${projectName}`;
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
        const userName = window.getUserName();
        try {
            const response = await fetch(`${protocol}//${domain}:${port}/services/DitenPPM/Workflow/DeleteWorkflow`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ workflowId: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.workflow-table').DataTable();
                table.ajax.reload(null, false);

                // Modalı kapat
                bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
            } else {
                showToast('Silme işlemi başarısız oldu.', "error");
                console.warn(result.errors); // Hata detayları varsa konsola yaz
            }
        } catch (error) {
            console.error(error);
            showToast('Bir hata oluştu.', "error");

        }
    });
}

function initializeFormValidation() {
    const workflowForm = document.getElementById('create-workflow');

    if (!workflowForm) return;

    const fv = FormValidation.formValidation(workflowForm, {
        fields: {
            recordType: {
                validators: {
                    notEmpty: {
                        message: 'Record Type is required'
                    }
                }
            },
            category: {
                validators: {
                    notEmpty: {
                        message: 'Category is required'
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

    handleFormSubmit(fv);
}

function handleFormSubmit(fv) {
    fv.on('core.form.valid', function () {

        const recordTypeSelect = document.getElementById('add-record-type');

        const recordTypeValue = recordTypeSelect.value;                
        const recordTypeText = recordTypeSelect.selectedOptions[0].text; 

        const categorySelect = document.getElementById('add-category');
        const categoryValue = categorySelect.value;
        const categoryText = categorySelect.selectedOptions[0].text; 

        window.location.href = `/ppm/blank/new-record?recordTypeId=${recordTypeValue}&recordType=${recordTypeText}&categoryId=${categoryValue}&category=${categoryText}`;

    });
}

$(document).on('click', '.edit-record', async function () {
    const id = $(this).data('id');

    //const recordTypeId = $(this).data('recordtypeid');
    //const recordType = $(this).data('recordtype');
    //const categoryId = $(this).data('categoryid');
    //const category = $(this).data('category');

    window.location.href = `/ppm/${id}/project-creation?mode=edit`;

});
$(document).on('click', '.view-record', async function () {
    const id = $(this).data('id');

    //const recordTypeId = $(this).data('recordtypeid');
    //const recordType = $(this).data('recordtype');
    //const categoryId = $(this).data('categoryid');
    //const category = $(this).data('category');

    window.location.href = `/ppm/${id}/project-creation?mode=view`;

});
function updateWorkflowDashboardCards(data) {

    const workflows = data || [];

    // 1) Toplam workflow sayısı
    const totalWorkflows = workflows.length;

    // 2) Active workflows (workflowStatusId = 2 veya senin aktif durumun hangisi ise)
    const activeCount = workflows.filter(w => Number(w.workflowStatusId) === 2).length;

    // 3) Completed workflows
    const completedCount = workflows.filter(w => Number(w.workflowStatusId) === 3).length;

    // 4) Average progress (sadece progress değeri olanları dahil et)
    const progressValues = workflows
        .map(w => Number(w.progress))
        .filter(p => !isNaN(p));

    const avgProgress = progressValues.length > 0
        ? Math.round(progressValues.reduce((a, b) => a + b, 0) / progressValues.length)
        : 0;

    // UI update
    document.getElementById("wfTotalCount").textContent = totalWorkflows;
    document.getElementById("wfActiveCount").textContent = `${activeCount} active`;
    document.getElementById("wfCompletedCount").textContent = completedCount;
    document.getElementById("wfAvgProgress").textContent = `${avgProgress}%`;
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

//------------------------------ DOWNLOAD------------------------------//
$(document).on("click", ".download-template", function () {
    $("#ddlTemplateType").select2({
        dropdownParent: $("#modalTemplateType"),
        width: '100%',
        placeholder: "Select template type",
        allowClear: false
    });
    loadInitiativeOptionsIntoDropdown();
    $("#modalTemplateType").modal("show");
});
function loadInitiativeOptionsIntoDropdown() {

    const userId = window.getUserId();
    const url = `${protocol}//${domain}:${port}/services/DitenPPM/Workflow/GetWorkflows?currentUserId=${userId}`;

    $.ajax({
        url: url,
        type: "GET",
        success: function (res) {

            const ddl = $("#ddlTemplateType");

            // önce Create seçeneği kalsın, diğerlerini temizleyelim
            ddl.find("option:not([value='create'])").remove();

            (res?.data || []).forEach(item => {
                ddl.append(`<option value="${item.id}">${item.name}</option>`);
            });
        },
        error: function (xhr) {
            console.error("Initiative list load failed:", xhr);
            showToast("Failed to load initiatives!", "error");
        }
    });
}




