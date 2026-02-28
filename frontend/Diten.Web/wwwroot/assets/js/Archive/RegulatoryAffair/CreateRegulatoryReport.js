'use strict';

dayjs.extend(dayjs_plugin_relativeTime);
let isGeneralSaved = false;
let savedRegulatoryReportId = "";
let uploadedFiles = [];
let uploadedTaskFiles = [];
const comments = [];
const port2 = protocol === 'https:' ? '5055' : '5050';
const port3 = protocol === 'https:' ? '5060' : '5053';
document.getElementById('uploadIcon').addEventListener('click', function () {
    document.getElementById('fileInput').click(); // ikon tıklanınca input tetiklenir
});

document.getElementById('fileInput').addEventListener('change', function (event) {
    const file = event.target.files[0];
    if (file) {
        uploadedFiles.push(file); // Dosyayı listeye ekle
        displayFile(file);     
        // Dosya seçildiğinde burada işlemler yapılabilir
        console.log("Seçilen dosya:", file.name);
        // İstersen hemen form-data ile sunucuya gönderebilirsin
    }
});

function displayFile(file) {
    const uploadItems = document.getElementById('uploadItems');

    const itemDiv = document.createElement('div');
    itemDiv.classList.add('d-flex', 'align-items-center', 'justify-content-between', 'mb-2', 'border', 'rounded', 'p-2');

    const fileName = document.createElement('span');
    fileName.textContent = file.name;
    fileName.classList.add('text-truncate', 'me-2');

    const buttonsDiv = document.createElement('div');
    buttonsDiv.classList.add('d-flex', 'align-items-center');

    const deleteBtn = document.createElement('a');
    deleteBtn.href = 'javascript:;';
    deleteBtn.innerHTML = '<i class="bx bx-trash text-danger"></i>';
    deleteBtn.title = 'Delete';
    deleteBtn.addEventListener('click', function () {
        uploadItems.removeChild(itemDiv);
        uploadedFiles = uploadedFiles.filter(f => f !== file); // Listeden çıkar
    });

    buttonsDiv.appendChild(deleteBtn);
    itemDiv.appendChild(fileName);
    itemDiv.appendChild(buttonsDiv);
    uploadItems.appendChild(itemDiv);
}

document.getElementById('uploadTaskIcon').addEventListener('click', function () {
    document.getElementById('fileTaskInput').click(); // ikon tıklanınca input tetiklenir
});
document.getElementById('fileTaskInput').addEventListener('change', function (event) {
    const file = event.target.files[0];
    if (file) {
        uploadedTaskFiles.push(file); // Dosyayı listeye ekle
        displayTaskFile(file);
        // Dosya seçildiğinde burada işlemler yapılabilir
        console.log("Seçilen dosya:", file.name);
        // İstersen hemen form-data ile sunucuya gönderebilirsin
    }
});

function displayTaskFile(file) {
    const uploadItems = document.getElementById('uploadTaskItems');

    const itemDiv = document.createElement('div');
    itemDiv.classList.add('d-flex', 'align-items-center', 'justify-content-between', 'mb-2', 'border', 'rounded', 'p-2');

    const fileName = document.createElement('span');
    fileName.textContent = file.name;
    fileName.classList.add('text-truncate', 'me-2');

    const buttonsDiv = document.createElement('div');
    buttonsDiv.classList.add('d-flex', 'align-items-center');

    const deleteBtn = document.createElement('a');
    deleteBtn.href = 'javascript:;';
    deleteBtn.innerHTML = '<i class="bx bx-trash text-danger"></i>';
    deleteBtn.title = 'Delete';
    deleteBtn.addEventListener('click', function () {
        uploadItems.removeChild(itemDiv);
        uploadedTaskFiles = uploadedTaskFiles.filter(f => f !== file); // Listeden çıkar
    });

    buttonsDiv.appendChild(deleteBtn);
    itemDiv.appendChild(fileName);
    itemDiv.appendChild(buttonsDiv);
    uploadItems.appendChild(itemDiv);
}




document.addEventListener('DOMContentLoaded', function () {
    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;


    const countryUrl = `${window.ApiBaseUrl}/services/PvTenant/Tenant/GetCountriesByTenantId`;
    fetchCountries(countryUrl, "ddlCountry");
    const regulatoryReportStatusUrl = `${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReport/GetRegulatoryReportStatus`;
    fetchDropdowns(regulatoryReportStatusUrl, "ddlRegulatoryReportStatus");
    const authorityUrl = `${window.ApiBaseUrl}/services/PvTenant/Authority/GetAuthoritiesByTenantId`;
    fetchDropdowns(authorityUrl, "ddlAuthority");
    initializeFormValidation();
    bindDeleteRecordEvent();
    if (id) {
        loadRegulatoryReport();

 
    }
    


});

function initDataTable(placeholderText, lanData) {

    const dt_regulatory_report_task_table = document.querySelector('.regulatory-task-table');
    if (dt_regulatory_report_task_table) {
        //savedRegulatoryReportId = "6820e540eb2d114c04827f98";
        if (savedRegulatoryReportId) {
            const dt_regulatory_report_task = new DataTable(dt_regulatory_report_task_table, {
                ajax: {
                    url: `${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReportTask/GetRegulatoryTasksByRegulatoryReportId/${savedRegulatoryReportId}`,
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
                    { data: 'statusStr' },
                    { data: 'priorityStr' },
                    { data: 'startDateStr' },
                    { data: 'endDateStr' },
                    { data: 'assignedToName' },
                    { data: 'parentTaskName' },
                    { data: null },
                ],
                columnDefs: [
                    {
                        className: 'control',
                        responsivePriority: 1,
                        searchable: false,
                        targets: 0,
                        render: function () {
                            return '';
                        }
                    },
                    {
                        targets: 2,
                        render: function (data, type, full, meta) {
                            let badgeClass = '';

                            switch (full.statusId) {
                                case 1:
                                    badgeClass = 'bg-label-warning'; // Sarı
                                    break;
                                case 2:
                                    badgeClass = 'bg-label-success'; // Yeşil
                                    break;
                                case 3:
                                    badgeClass = 'bg-label-danger'; // Kırmızı
                                    break;
                                default:
                                    badgeClass = 'bg-label-secondary'; // Varsayılan (gri)
                            }

                            return `<span class="badge ${badgeClass} text-capitalized">${data}</span>`;
                        }
                    },
                    {
                        targets: 3,
                        render: function (data, type, full, meta) {
                            let badgeClass = '';

                            switch (full.priorityId) {
                                case 1:
                                    badgeClass = 'text-bg-primary'; // Sarı
                                    break;
                                case 2:
                                    badgeClass = 'text-bg-secondary'; // Yeşil
                                    break;
                                case 3:
                                    badgeClass = 'text-bg-danger'; // Kırmızı
                                    break;
                                default:
                                    badgeClass = 'text-bg-secondary'; // Varsayılan (gri)
                            }

                            return `<span class="badge badge-dot ${badgeClass} me-1"></span> ${data}`;
                        }
                    },
                    {
                        targets: 6,
                        responsivePriority: 3,
                        render: function (data, type, full, meta) {
                            var name = full['assignedToName'];
                            var output;


                            // For Avatar badge
                            var stateNum = Math.floor(Math.random() * 6);
                            var states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
                            var state = states[stateNum];
                            var initials = (name.split(' ').map(word => word[0]).join('')).toUpperCase();
                            output = '<span class="avatar-initial rounded-circle bg-label-' + state + '">' + initials + '</span>';


                            // Creates full output for row
                            var row_output =
                                '<div class="d-flex justify-content-start align-items-center user-name">' +
                                '<div class="avatar-wrapper">' +
                                '<div class="avatar avatar-sm me-4">' +
                                output +
                                '</div>' +
                                '</div>' +
                                '</div>';
                            return row_output;
                        }
                    },

                    {
                        targets: -1,
                        title: 'Actions',
                        searchable: false,
                        orderable: false,
                        render: (data, type, full) => {
                            return `
                            <div class="d-flex align-items-center">
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
                                        text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block" data-i18n="AddTask">Add Task</span>',
                                        className: 'add-new btn btn-primary',
                                        action: function (e, dt, node, config) {
                                            AddTaskView();
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
                    modifyDataTableLayout(); // tablo ilk yüklendiğinde stil uygula
                  
                },
                drawCallback: function () {
                    modifyDataTableLayout(); // her yeniden çizimde stil uygula (sayfalama, filtre vs.)
                }
            });

        }

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


function showTab(tab) {

   

    // Butonları güncelle
    document.querySelectorAll('.nav-link').forEach(el => {
        el.classList.remove('active');
        if (el.dataset.tab === tab) {
            el.classList.add('active');
        }
    });

    const tabGeneral = document.getElementById('tab-general');
    const tabAction = document.getElementById('tab-action');

    if (tab === 'general') {
        document.querySelectorAll('.nav-link')[0].classList.add('active');
        tabGeneral.classList.remove('hidden');
        tabAction.classList.add('hidden');
    } else {
        //if (!isGeneralSaved) {

        //    showToast('Please save the general information first.', "error");
        //    return;
        //}
        document.querySelectorAll('.nav-link')[1].classList.add('active');
        tabGeneral.classList.add('hidden');
        tabAction.classList.remove('hidden');
    }
}

async function fetchCountries(apiUrl, selectElementId) {
    try {
        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const selectElement = document.getElementById(selectElementId);

        // Select2 varsa önce destroy et (varsa)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).empty().trigger('change');
        } else {
            selectElement.innerHTML = '';
        }

        // İlk boş option
        const defaultOption = new Option("Select a country", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(country => {
            const value = country.id ?? country.iso2 ?? country.name;
            const option = new Option(country.name, value, false, false);
            selectElement.appendChild(option);
        });

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Ülkeler alınırken hata oluştu:", error);
    }
}

async function fetchDropdowns(apiUrl, selectElementId) {
    try {
        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const selectElement = document.getElementById(selectElementId);

        // Select2 varsa önce destroy et (varsa)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).empty().trigger('change');
        } else {
            selectElement.innerHTML = '';
        }

        // İlk boş option
        const defaultOption = new Option("Select a status", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(status => {
            const value = status.id ?? status.name;
            const color = status.id == 1 ? 'green' :
                status.id == 2 ? 'red' : 'black';
            const option = new Option(status.name, value, false, false);
            option.style.color = color;
            selectElement.appendChild(option);
        });

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Ülkeler alınırken hata oluştu:", error);
    }
}

async function fetchUserDropdowns(apiUrl, selectElementId) {
    try {
        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const selectElement = document.getElementById(selectElementId);

        // Select2 varsa önce destroy et (varsa)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).empty().trigger('change');
        } else {
            selectElement.innerHTML = '';
        }

        // İlk boş option
        const defaultOption = new Option("Select a user", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(user => {
            const value = user.id ?? user.fullName;
            const color = user.id == 1 ? 'green' :
                user.id == 2 ? 'red' : 'black';
            const option = new Option(user.fullName, value, false, false);
            option.style.color = color;
            selectElement.appendChild(option);
        });

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Ülkeler alınırken hata oluştu:", error);
    }
}

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

function initializeFormValidation() {
    const addRegulatoryReportForm = document.getElementById('general-form');

    if (!addRegulatoryReportForm) return;

    const fv = FormValidation.formValidation(addRegulatoryReportForm, {
        fields: {
            ddlCountry: {
                validators: {
                    notEmpty: {
                        message: 'Please select a country'
                    }
                }
            },
            ddlRegulatoryReportStatus: {
                validators: {
                    notEmpty: {
                        message: 'Please select a status'
                    }
                }
            },
            ddlAuthority: {
                validators: {
                    notEmpty: {
                        message: 'Please select a instution'
                    }
                }
            },
            datePublish: {
                validators: {
                    notEmpty: {
                        message: 'Please input publish date'
                    }
                }
            },

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

function saveGeneral() {
    const userName = window.getUserName();
    const userId = window.getUserId();
    const countryId = document.getElementById('ddlCountry').value;
    const regulatoryReportStatusId = document.getElementById('ddlRegulatoryReportStatus').value;
    const authorityId = document.getElementById('ddlAuthority').value;
    const fltpcker = document.querySelector('#dtPublish')._flatpickr;
    const selectedDate = fltpcker.selectedDates[0];
    let isoDate;
    if (selectedDate) {
        isoDate = new Date(selectedDate.getTime() - selectedDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }
    const linkToUpdate = document.getElementById('txtLink').value;
    const summaryEnglish = window.snowEditor.root.innerHTML;
    const comments = window.commentEditor.root.innerHTML;

    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;

    if (!id) {
        const formData = {
            countryId: countryId,
            authorityId: authorityId,
            publishDate: isoDate,
            linkUpdate: linkToUpdate,
            summary: summaryEnglish,
            regulatoryStatus: regulatoryReportStatusId,
            createdBy: userName,
            createdUserId: userId,
            comment: comments,

        };


        fetch(`${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReport/CreateRegulatoryReport`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        })
            .then(response => response.json())
            .then(data => {

                const isSuccess = data.errors === null;
                if (isSuccess) {
                    //window.location.href = '/master-data/global-sku'; isGeneralSaved = true;
                    savedRegulatoryReportId = data.data;
                    const formUploadData = new FormData();
                    formUploadData.append('RegulatoryReportId', savedRegulatoryReportId);
                    uploadedFiles.forEach(file => {
                        formUploadData.append('Files', file); // API tarafında List<IFormFile> olarak karşılanacak
                    });
                    fetch(`${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReport/UpdateRegulatoryReportDocuments`, {
                        method: 'POST',
                        body: formUploadData
                    })
                        .then(response => response.json())
                        .then(result => {

                            if (result.data) {
                                showToast('Regulatory report successfully added', "success");
                                isGeneralSaved = true;
                                const lang = localStorage.getItem('language') || 'en';

                                fetch(`/assets/lang/${lang}.json`)
                                    .then(response => response.json())
                                    .then(data => {
                                        const placeholderText = data["SearchTask"] || "Search Task";

                                        // DataTable veya custom tablo init fonksiyonunu burada çağır:
                                        initDataTable(placeholderText, data);
                                    })
                                    .catch(error => {
                                        console.error('Language file could not be loaded:', error);
                                        initDataTable("Search Task", data); // fallback
                                    });
                            }
                            else {
                                const errorMessage = data.errors?.join('<br>') || 'An error occurred during the update.';
                                showToast(errorMessage, "error");
                            }




                        })
                        .catch(error => {

                            console.error(error);
                            showToast('An unexpected error occurred.', "error");
                        });



                }
                else {
                    const errorMessage = data.errors?.join('<br>') || 'An error occurred during the update.';
                    showToast(errorMessage, "error");
                }






            })
            .catch(error => {
                console.error(error);
                showToast('An unexpected error occurred.', "error");

            });
    }
    else {
        const formData = {
            countryId: countryId,
            authorityId: authorityId,
            publishDate: isoDate,
            linkUpdate: linkToUpdate,
            summary: summaryEnglish,
            regulatoryStatus: regulatoryReportStatusId,
            createdBy: userName,
            createdUserId: userId,
            comment: comments,

        };


        fetch(`${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReport/CreateRegulatoryReport`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        })
            .then(response => response.json())
            .then(data => {

                const isSuccess = data.errors === null;
                if (isSuccess) {
                    //window.location.href = '/master-data/global-sku'; isGeneralSaved = true;
                    savedRegulatoryReportId = data.data;
                    const formUploadData = new FormData();
                    formUploadData.append('RegulatoryReportId', savedRegulatoryReportId);
                    uploadedFiles.forEach(file => {
                        formUploadData.append('Files', file); // API tarafında List<IFormFile> olarak karşılanacak
                    });
                    fetch(`${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReport/UpdateRegulatoryReportDocuments`, {
                        method: 'POST',
                        body: formUploadData
                    })
                        .then(response => response.json())
                        .then(result => {

                            if (result.data) {
                                showToast('Regulatory report successfully added', "success");
                                isGeneralSaved = true;
                                const lang = localStorage.getItem('language') || 'en';

                                fetch(`/assets/lang/${lang}.json`)
                                    .then(response => response.json())
                                    .then(data => {
                                        const placeholderText = data["SearchTask"] || "Search Task";

                                        // DataTable veya custom tablo init fonksiyonunu burada çağır:
                                        initDataTable(placeholderText, data);
                                    })
                                    .catch(error => {
                                        console.error('Language file could not be loaded:', error);
                                        initDataTable("Search Task", data); // fallback
                                    });
                            }
                            else {
                                const errorMessage = data.errors?.join('<br>') || 'An error occurred during the update.';
                                showToast(errorMessage, "error");
                            }




                        })
                        .catch(error => {

                            console.error(error);
                            showToast('An unexpected error occurred.', "error");
                        });



                }
                else {
                    const errorMessage = data.errors?.join('<br>') || 'An error occurred during the update.';
                    showToast(errorMessage, "error");
                }






            })
            .catch(error => {
                console.error(error);
                showToast('An unexpected error occurred.', "error");

            });
    }


   
}

function AddTaskView() {

    var getMainView = document.getElementById('mainView');
    var getAddTaskView = document.getElementById('addNewTaskView');
    uploadedTaskFiles.length = 0;
    comments.length = 0;
    const taskStatusUrl = `${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReportTask/GetRegulatoryTaskStatus`;
    fetchDropdowns(taskStatusUrl, "ddlTaskStatus");

    const taskPriorityUrl = `${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReportTask/GetRegulatoryTaskPriority`;
    fetchDropdowns(taskPriorityUrl, "ddlPriority");

    const taskParentTaskUrl = `${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReportTask/GetRegulatoryParentTasksByRegulatoryReportId/${savedRegulatoryReportId}`;
    fetchDropdowns(taskParentTaskUrl, "ddlParentTask");

    const taskAssignedToUrl = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByCompanyId`;
    fetchUserDropdowns(taskAssignedToUrl, "ddlAssignedTo");

    const userName = window.getUserName();

    const states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
    const state = states[Math.floor(Math.random() * states.length)];

    const initials = userName
        .split(' ')
        .map(word => word[0])
        .join('')
        .toUpperCase();

    const avatarSpan = document.getElementById('avatarInitials');
    avatarSpan.textContent = initials;
    avatarSpan.classList.add('bg-label-' + state);

    $('#ddlTaskStatus').val('').trigger('change');
    $('#ddlPriority').val('').trigger('change');
    $('#ddlAssignedTo').val('').trigger('change');
    $('#txtTaskName').val('');
    $('#ddlParentTask').val('').trigger('change');

    const dtStartDate = document.querySelector('#dtStartDate');
    const flatpickrdtStartDate =dtStartDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    flatpickrdtStartDate.clear();

    const dtEndDate = document.querySelector('#dtEndDate');
    const flatpickrdtEndDate = dtEndDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    flatpickrdtEndDate.clear();


    window.txtDescription.root.innerHTML = '';






    getAddTaskView.classList.remove('hidden');
    getMainView.classList.add('hidden');

}
function CancelTaskView() {

    var getMainView = document.getElementById('mainView');
    var getAddTaskView = document.getElementById('addNewTaskView');

    getMainView.classList.remove('hidden');
    getAddTaskView.classList.add('hidden');


}

document.addEventListener("DOMContentLoaded", function () {
    const userName = window.getUserName();
    const states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
    const state = states[Math.floor(Math.random() * states.length)];
    const initials = userName.split(' ').map(w => w[0]).join('').toUpperCase();

    const avatarSpan = document.getElementById('avatarInitials');
    avatarSpan.textContent = initials;
    avatarSpan.classList.add('bg-label-' + state);

    document.getElementById("btnComment").addEventListener("click", function () {
        const reply = document.getElementById("txtReply").value.trim();
        if (reply === "") return;

        const now = dayjs();
        const timeAgo = now.fromNow(); // Örn: "a few seconds ago", "5 minutes ago"

        const commentData = {
            commentedByName: userName,
            state: state,
            commentTitle: reply,
            commentDescription: reply,
            commentedTime: now.toISOString()
        };
        comments.push(commentData);
        const newItem = `
      <li class="timeline-item timeline-item-transparent">
        <span class="timeline-point timeline-point-primary" style="box-shadow:none;"></span>
        <div class="timeline-event">
          <div class="timeline-header mb-3">
            <h6 class="mb-0">${reply}</h6>
            <small class="text-body-secondary">${timeAgo}</small>
          </div>
          <p class="mb-2">${reply}</p>
          <div class="d-flex justify-content-between flex-wrap gap-2 mb-2">
            <div class="d-flex flex-wrap align-items-center mb-50">
              <div class="avatar avatar-sm me-2">
                <span class="avatar-initial rounded-circle bg-label-${state} text-uppercase">${initials}</span>
              </div>
              <div>
                <p class="mb-0 small fw-medium">${userName} (Commented)</p>
              </div>
            </div>
          </div>
        </div>
      </li>
    `;

        document.getElementById("timelineList").insertAdjacentHTML("afterbegin", newItem);
        document.getElementById("txtReply").value = "";
    });
});


function saveTask() {
    const userName = window.getUserName();
    const userId = window.getUserId();
    const taskName = document.getElementById('txtTaskName').value;
    const taskStatusId = document.getElementById('ddlTaskStatus').value;
    const taskPriorityId = document.getElementById('ddlPriority').value;
    const assignedToId = document.getElementById('ddlAssignedTo').value;
    const parentTaskId = document.getElementById('ddlParentTask')?.value || "";
    const fltpckerStart = document.querySelector('#dtStartDate')._flatpickr;
    const selectedDate = fltpckerStart.selectedDates[0];
    let isoDate;
    if (selectedDate) {
        isoDate = new Date(selectedDate.getTime() - selectedDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }

    const fltpckerEnd = document.querySelector('#dtEndDate')._flatpickr;
    const selectedDateEnd = fltpckerEnd.selectedDates[0];
    let isoDateEnd;
    if (selectedDateEnd) {
        isoDateEnd = new Date(selectedDateEnd.getTime() - selectedDateEnd.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }
    const isGroup = document.getElementById('chcIsGroup').checked;

    const description = window.txtDescription.root.innerHTML;

    const formData = {
        regulatoryReportId: savedRegulatoryReportId,
        name: taskName,
        statusId: taskStatusId,
        priorityId: taskPriorityId,
        startDate: isoDate,
        endDate: isoDateEnd,
        assignedToId: assignedToId,
        assignedFromId: userId,
        parentTaskId: parentTaskId,
        isGroup: isGroup,
        description: description,
        createdBy: userName,
        createdUserId: userId,
        comments: comments,

    };


    fetch(`${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReportTask/CreateRegulatoryTask`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(formData)
    })
        .then(response => response.json())
        .then(data => {

            const isSuccess = data.errors === null;
            if (isSuccess) {
                
                const formTaskUploadData = new FormData();
                formTaskUploadData.append('RegulatoryTaskId', data.data);
                if (uploadedTaskFiles.length > 0) {
                    uploadedTaskFiles.forEach(file => {
                        formTaskUploadData.append('Files', file); // API tarafında List<IFormFile> olarak karşılanacak
                    });
                }
                
                fetch(`${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReportTask/UpdateRegulatoryTaskDocuments`, {
                    method: 'POST',
                    body: formTaskUploadData
                })
                    .then(response => response.json())
                    .then(result => {

                        if (result.data) {
                            var getMainView = document.getElementById('mainView');
                            var getAddTaskView = document.getElementById('addNewTaskView');

                            const table = $('.regulatory-task-table').DataTable();
                            table.ajax.reload();

                            getMainView.classList.remove('hidden');
                            getAddTaskView.classList.add('hidden');

                        }
                        else {
                            const errorMessage = data.errors?.join('<br>') || 'An error occurred during the update.';
                            showToast(errorMessage, "error");
                        }




                    })
                    .catch(error => {

                        console.error(error);
                        showToast('An unexpected error occurred.', "error");
                    });



            }
            else {
                const errorMessage = data.errors?.join('<br>') || 'An error occurred during the update.';
                showToast(errorMessage, "error");
            }






        })
        .catch(error => {
            console.error(error);
            showToast('An unexpected error occurred.', "error");

        });

}

async function loadRegulatoryReport() {

    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;

    
    const response = await fetch(`${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReport/GetRegulatoryReportById/${id}`); // API adresin 
    const result = await response.json();
    const item = result.data;

    $('#ddlCountry').val(item.countryId).trigger('change');
    $('#ddlRegulatoryReportStatus').val(item.regulatoryReportStatusId).trigger('change');
    $('#ddlAuthority').val(item.authorityId).trigger('change');
    $('#txtLink').val(item.linkToUpdate);
    $('#add-ddlCompany').val(item.companyId).trigger('change');
    $('#add-ddlProductionSite').val(item.productionSideCompanyId).trigger('change');
    $('#add-ddlPackagingSite').val(item.packagingSiteCompanyId).trigger('change');
    $('#add-ddlBatchReleaseSite').val(item.batchReleaseSiteCompanyId).trigger('change');
    const flatpickrFriendly = document.querySelector('#dtPublish');
    flatpickrFriendly.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    flatpickrFriendly._flatpickr.setDate(item.publishDate, false);
    window.snowEditor.root.innerHTML = item.summary;
    window.commentEditor.root.innerHTML = item.comment;
    isGeneralSaved = true;
    savedRegulatoryReportId = id;
    displayUploadedFiles(item.documents);
    const lang = localStorage.getItem('language') || 'en';

    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["SearchTask"] || "Search Task";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Language file could not be loaded:', error);
            initDataTable("Search Task", data); // fallback
        });
}
function displayUploadedFiles(documents) {
    const uploadItems = document.getElementById('uploadItems');
    documents.forEach(doc => {
        const itemDiv = document.createElement('div');
        itemDiv.classList.add('d-flex', 'align-items-center', 'justify-content-between', 'mb-2', 'border', 'rounded', 'p-2');

        const fileName = document.createElement('span');
        fileName.textContent = doc.documentName;
        fileName.classList.add('text-truncate', 'me-2');

        const buttonsDiv = document.createElement('div');
        buttonsDiv.classList.add('d-flex', 'align-items-center');

        // İndirme butonu oluşturma
        const downloadBtn = document.createElement('a');
        downloadBtn.href = `${protocol}//${domain}:${port3}${doc.filePath}`;

        //downloadBtn.href = `C:/DitenPvOrganization/wwwroot/RegulatoryReport/${document.file}`; // Dosyanın doğru yolu
        downloadBtn.download = doc.documentName; // Dosya indirilirken adı
        downloadBtn.innerHTML = '<i class="bx bx-download text-success"></i>';
        downloadBtn.title = 'Download';
        downloadBtn.classList.add('me-2');

        // Silme butonu oluşturma
        const deleteBtn = document.createElement('a');
        deleteBtn.href = 'javascript:;';
        deleteBtn.innerHTML = '<i class="bx bx-trash text-danger"></i>';
        deleteBtn.title = 'Delete';
        deleteBtn.addEventListener('click', function () {
            uploadItems.removeChild(itemDiv);
            // Burada dosya silme işlemi yapabilirsiniz
        });

        buttonsDiv.appendChild(downloadBtn);
        buttonsDiv.appendChild(deleteBtn);
        itemDiv.appendChild(fileName);
        itemDiv.appendChild(buttonsDiv);
        uploadItems.appendChild(itemDiv);
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
            const response = await fetch(`${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReportTask/DeleteRegulatoryTask`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.regulatory-task-table').DataTable();
                table.ajax.reload(null, false);

                // Modalı kapat
                bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
            } else {
                showToast('The delete operation could not be completed.', "error");
                console.warn(result.errors); // Hata detayları varsa konsola yaz
            }
        } catch (error) {
            console.error(error);
            showToast('An unexpected error occurred.', "error");

        }
    });
}
