'use strict';
const userName = window.getUserName();
let dt_survey_list_table;
let dt_survey_list;
const createForm = document.getElementById('createSurveyList');
let dummyData = [
    {
        id: 1,
        name: "Customer Satisfaction Survey",
        useApplication: "Survey",
        type: "Customer Feedback",
        duration: "7 min",
        targetAudience: "Customers",
        language: "English",
        activeStatus: 1
    },
    {
        id: 2,
        name: "Employee Engagement Survey",
        useApplication: "Survey",
        type: "Internal",
        duration: "10 min",
        targetAudience: "Employees",
        language: "Turkish",
        activeStatus: 2
    },
    {
        id: 3,
        name: "Market Research – Product X",
        useApplication: "Survey",
        type: "Research",
        duration: "15 min",
        targetAudience: "Potential Buyers",
        language: "English",
        activeStatus: 1
    },
    {
        id: 4,
        name: "Post-Event Feedback Form",
        useApplication: "Survey",
        type: "Event Evaluation",
        duration: "5 min",
        targetAudience: "Event Participants",
        language: "French",
        activeStatus: 1
    },
    {
        id: 5,
        name: "Training Effectiveness Survey",
        useApplication: "Survey",
        type: "Performance",
        duration: "8 min",
        targetAudience: "Employees",
        language: "Turkish",
        activeStatus: 2
    }
];


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

    initializeFormValidation();
    initializeCharacterCounter('add-description', 2000);


    populateSelect('add-target-auidence', {
        apiUrl: `${window.ApiBaseUrl}/services/PvSurvey/Survey/GetTargetAudiences`,
        placeholder: 'Select target auidence',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer
        
    });

    populateSelect('add-survey-type', {
        apiUrl: `${window.ApiBaseUrl}/services/PvSurvey/SurveyType/GetSurveyTypes`,
        placeholder: 'Select survey type...',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

    });

    populateSelect('add-language', {
        apiUrl: `${window.ApiBaseUrl}/services/PvTenant/Language/GetLanguages`,
        placeholder: 'Select language',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

    });

});

function initDataTable(placeholderText, lanData) {

    dt_survey_list_table = document.querySelector('.survey-list-table');

    if (dt_survey_list_table) {
        dt_survey_list = new DataTable(dt_survey_list_table, {

            ajax: {
                url: `${window.ApiBaseUrl}/services/PvSurvey/Survey/GetSurveys`,
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
                {
                    data: 'application',
                    render: function (data, type, row) {
                        let badgeColor = 'primary'; // Varsayılan
                        const appName = data ? data.name : '-';
                        switch (appName) {
                            case 'Survey':
                                badgeColor = 'secondary';
                                break;
                            case 'CRO':
                                badgeColor = 'warning';
                                break;
                            default:
                                badgeColor = 'primary';
                                break;
                        }

                        return `<span class="badge badge-outline-${badgeColor}">${appName || '-'}</span>`;
                    }
                },
                {
                    data: 'surveyType',
                    render: function (data, type, row) {

                        const typeName = data ? data.name : '-';

                        var stateNum = Math.floor(Math.random() * 6);
                        var states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
                        var state = states[stateNum];

                        return `<span class="badge bg-label-${state}">${typeName || '-'}</span>`;
                    }
                },
                {
                    data: 'duration',
                    render: d => d ? `${d} min` : '-'
                },
                {
                    data: 'targetAudience',
                    render: function (data, type, row) {
                            return data ? data.name : '-';
                    }


                },
                {
                    data: 'language',
                    render: function (data, type, row) {
                        return data ? data.name : '-';
                    }
                },
                { data: 'surveyStatus' },
                { data: null },
            ],
            columnDefs: [
                {
                    className: 'control',
                    responsivePriority: 2,
                    searchable: true,
                    targets: 0,
                    visible: false,
                    render: function (data) {
                        return data; // id değeri görünsün
                    }
                },
                {
                    targets: 1,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        const name = full['name'];
                        const description = full['description'];
                        // Creates full output for survey name and description
                        const rowOutput = `
              <div class="d-flex justify-content-start align-items-center survey-name">
                <div class="d-flex flex-column text-wrap">
                  <span class="fw-medium">${name}</span>
                  <small class="text-break survey-desc">${description}</small>
                </div>
              </div>`;
                        return rowOutput;
                    }
                },
                {
                    targets: 7, // surveyStatus sütunu
                    render: function (data, type, full, meta) {

                        return data === true
                            ? `<span class="badge bg-label-primary text-capitalized">active</span>`
                            : `<span class="badge bg-label-secondary text-capitalized">draft</span>`;
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 1,
                    searchable: false,
                    orderable: false,
                    render: function (data, type, full, meta) {
                        const surveyStatus = full.surveyStatus;
                        let buttons = '';
                        let dropdownItems = '';


                        // Draft menü öğeleri
                        if (surveyStatus === false) {
                            dropdownItems += `
            <a href="javascript:void(0);" class="dropdown-item status-button edit-record" data-id="${full.id}" data-action="edit">
    <i class="icon-base bx bx-edit-alt icon-md"></i> Edit
</a>

            <a href="javascript:void(0);" class="dropdown-item status-button manage-record" data-id="${full.id}" data-activeStatus="${full.activeStatus}" data-isanyquestions="${full.isAnyQuestions}" data-action="manage">
                <i class="icon-base bx bx-slider-alt icon-md"></i> Manage
            </a>

            <a href="javascript:void(0);" class="dropdown-item status-button text-success activate-record" data-id="${full.id}" data-isanyquestions="${full.isAnyQuestions}" data-action="activate">
                <i class="icon-base bx bx-check-circle icon-md"></i> Activate
            </a>

            `;
                        }


                        dropdownItems += `
            <a href="javascript:void(0);" class="dropdown-item status-button preview-record" data-id="${full.id}" data-isanyquestions="${full.isAnyQuestions}" data-action="preview">
    <i class="icon-base bx bx-right-arrow icon-md"></i> Preview
</a>

            <a href="javascript:void(0);" class="dropdown-item status-button text-danger delete-record" data-id="${full.id}" data-activeStatus="${full.activeStatus}" data-action="delete">
                <i class="icon-base bx bx-trash icon-md"></i> Delete
            </a>`;



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
            ${buttons}
            ${dropdown}
        </div>`;
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
                                                columns: [1, 2,3,4,5,6],
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
                                                columns: [1, 2,3,4,5,6],
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
                                                columns: [1, 2,3,4,5,6],
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
                                                columns: [1, 2,3,4,5,6],
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
                                                columns: [1, 2,3,4,5,6],
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
                                    text: '<i class="icon-base bx bx-plus"></i><span class="d-none d-sm-inline-block">New Survey</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#offcanvasCreateSurveyList'
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
                sInfo: lanData.DataTable.sInfo,
                sInfoEmpty: lanData.DataTable.sInfoEmpty,
                sInfoFiltered: lanData.DataTable.sInfoFiltered,
                sLengthMenu: lanData.DataTable.sLengthMenu
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

                const params = new URLSearchParams(window.location.search);
                const filterSurveyId = params.get('filterSurveyId');

                const data = dt_survey_list.rows().data().toArray();

                // ID'si eşleşen satırı bul
                const matched = data.find(item => item.id === filterSurveyId);

                if (matched) {
                    const surveyName = matched.name;

                    // Arama kutusuna survey adını yaz
                    $('.dt-search input').val(surveyName);

                    // DataTable'da filtre uygula
                    dt_survey_list.search(surveyName).draw();
                }


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












function initializeFormValidation() {
    const createSurveyListForm = document.getElementById('createSurveyList');

    if (!createSurveyListForm) return;

    const fv = FormValidation.formValidation(createSurveyListForm, {
        fields: {
            surveyListName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter name'
                    },
                    stringLength: {
                        min: 3,
                        max: 250,
                        message: 'Name must be between 3 and 250 characters'
                    }
                }
            },
            surveyType: {
                validators: {
                    notEmpty: {
                        message: 'Survey Type is required'
                    }
                }
            },
            targetAuidence: {
                validators: {
                    notEmpty: {
                        message: 'Target Audience is required'
                    }
                }
            },
            language: {
                validators: {
                    notEmpty: {
                        message: 'Language is required'
                    }
                }
            },
            description: {
                validators: {
                    stringLength: {
                        max: 2000,
                        message: 'Description cannot exceed 2000 characters'
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

        const name = document.getElementById('add-name').value;
        const description = document.getElementById('add-description')?.value || "";
        const surveyType = document.getElementById('add-survey-type').value;
        const targetAudience = document.getElementById('add-target-auidence').value;
        const language = document.getElementById('add-language').value;
        const duration = document.getElementById('add-duration').value.trim();

        const formData = new FormData();
        formData.append("name", name);
        if (description.trim() !== "") {  // boş değilse ekle
            formData.append("description", description);
        }
        formData.append("surveyTypeId", surveyType);
        formData.append("targetAuidienceId", targetAudience);
        formData.append("languageId", language);
        formData.append("duration", duration);
        formData.append("applicationName", "Survey");
        formData.append("createdBy", userName);

        fetch(`${window.ApiBaseUrl}/services/PvSurvey/Survey/CreateSurvey`, {
            method: 'POST',
            body: formData
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                document.getElementById('add-description').value = '';
                const table = $('.survey-list-table').DataTable();
                table.ajax.reload();

                const durationInput = document.getElementById('add-duration');
                if (durationInput) {
                    durationInput.value = '';
                }
                // Select2 kullanıyorsan refresh
                ['add-survey-type', 'add-target-auidence', 'add-language'].forEach(id => {
                    const select = document.getElementById(id);
                    if (typeof $ !== 'undefined' && $(select).hasClass('select2')) {
                        $(select).val(null).trigger('change.select2');
                    }
                });

                showToast('The record has been added successfully.', "success");
            })
            .catch(error => {
                console.error(error);
                alert('Kayıt sırasında bir hata oluştu.');
            });



       
       



    });
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

let recordIdToDelete = null;

$(document).on('click', '.delete-record', function () {
    recordIdToDelete = $(this).data('id');

    const deleteModal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
    deleteModal.show();
});

$(document).on('click', '.manage-record', function () {
    const id = $(this).data('id');
    const isAnyQuestions = $(this).data('isanyquestions'); // data-isAnyQuestions attribute'u küçük harfe çevrilerek kullanılır

    if (isAnyQuestions) {
        // Eğer true ise bir yere yönlendir
        window.location.href = `/survey/${id}/manage?templateId=blank`;
    } else {
        // Eğer false ise başka yere yönlendir
        window.location.href = `/survey/${id}/manage-template`;
    }
});

$(document).on('click', '.preview-record', function () {
    const id = $(this).data('id');
    
    
        // Eğer false ise başka yere yönlendir
        window.location.href = `/survey/${id}/preview`;
    
});


$(document).on('click', '.activate-record',async function () {
    const id = $(this).data('id');
    const isAnyQuestions = $(this).data('isanyquestions'); // data-isAnyQuestions attribute'u küçük harfe çevrilerek kullanılır

    if (!isAnyQuestions) {
        // Eğer true ise bir yere yönlendir
        showToast('You cannot activate this survey because no questions have been added.', "error");
    }
    else {

        const activateData = new FormData();
        activateData.append("id", id);
        activateData.append("modifiedBy", userName);

        try {
            const response = await fetch(`${window.ApiBaseUrl}/services/PvSurvey/Survey/ActivateSurvey`, {
                method: 'POST',
                body: activateData
            });

            const result = await response.json();

            if (result.isSuccessful || result.data === true) {
                const table = $('.survey-list-table').DataTable();
                table.ajax.reload();

                showToast('The record has been activated successfully.', "success");



            } else {
                showToast('Active işlemi başarısız oldu.', "error");
                console.warn(result.errors); // Hata detayları varsa konsola yaz
            }
        } catch (error) {
            console.error(error);
            showToast('Bir hata oluştu.', "error");
        }

    }




});


document.getElementById('confirmDeleteBtn').addEventListener('click', async function () {
    if (!recordIdToDelete) return;

    const deletedData = new FormData();
    deletedData.append("id", recordIdToDelete);
    deletedData.append("modifiedBy", userName);

    try {
        const response = await fetch(`${window.ApiBaseUrl}/services/PvSurvey/Survey/DeleteSurvey`, {
            method: 'POST',
            body: deletedData
        });

        const result = await response.json();

        if (result.isSuccessful || result.data === true) {
            const table = $('.survey-list-table').DataTable();
            table.ajax.reload();

            // Modalı kapat
            bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();

            showToast('The record has been deleted successfully.', "error");



        } else {
            showToast('Silme işlemi başarısız oldu.', "error");
            console.warn(result.errors); // Hata detayları varsa konsola yaz
        }
    } catch (error) {
        console.error(error);
        showToast('Bir hata oluştu.', "error");
    }
});

function bindDeleteRecordEvent() {
   

}


function bindModalEvents() {
    document.addEventListener('show.bs.modal', function (event) {
        if (event.target.classList.contains('dtr-bs-modal')) {
            bindDeleteRecordEvent();
        }
    });

    document.addEventListener('hide.bs.modal', function (event) {
        if (event.target.classList.contains('dtr-bs-modal')) {
            bindDeleteRecordEvent();
        }
    });
}

