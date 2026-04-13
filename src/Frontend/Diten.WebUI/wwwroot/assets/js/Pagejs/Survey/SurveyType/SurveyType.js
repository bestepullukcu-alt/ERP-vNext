'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';




document.addEventListener('DOMContentLoaded', function () {

    const lang = localStorage.getItem('language') || 'en';
    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["Search Survey Type"] || "Search Survey Type";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Language file could not be loaded:', error);
            initDataTable("Search Survey Type", data); // fallback
        });

    initializeFormValidation();
    bindDeleteRecordEvent();
    bindModalEvents();
    initEdit();
    initUpdateFormValidation();
    initActiveDisableSurveyType();
});

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


function initDataTable(placeholderText, lanData) {

    const dt_survey_type_table = document.querySelector('.survey-type-table');

    if (dt_survey_type_table) {
        const dt_survey_type = new DataTable(dt_survey_type_table, {

            ajax: {
                url: `${protocol}//${domain}:${port}/services/PvSurvey/SurveyType/GetSurveyTypes`,
                method: 'GET',
                dataSrc: 'data',
                //error: function (jqxhr, textStatus, errorThrown) {
                //    // Bu callback sadece DataTable özelinde hata olursa çalışır
                //    console.error("DataTable Error:", jqxhr.status, errorThrown);
                //    if (jqxhr.status !== 200) {
                //        window.location.href = '/pages-misc-error.html?code=' + jqxhr.status; // Hata kodu ile yönlendirme
                //    }
                //}
            },
            columns: [
                { data: 'id' },
                { data: 'name' },
                { data: 'description' },
                { data: 'isActive' },
                { data: null },
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
                    targets: 3, // isActiveStr sütunu
                    render: function (data, type, full, meta) {

                        return data === true
                            ? `<span class="badge bg-label-success text-capitalized">active</span>`
                            : `<span class="badge bg-label-secondary text-capitalized">disabled</span>`;
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
                    render: (data, type, full) => {
                        return `
                            <div class="d-flex align-items-center">
                                <a href="javascript:;" class="btn btn-icon activate-disabled-record" data-id="${full.id}" data-status="${full.isActive}" data-name="${full.name}" data-description="${full.description}">
                                    <i class="icon-base bx bx-power-off icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon edit-record" data-id="${full.id}" data-name="${full.name}" data-description="${full.description}" data-status="${full.isActive}">
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
                                                columns: [0, 1, 2],
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
                                                columns: [0, 1, 2],
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
                                                columns: [0, 1, 2],
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
                                                columns: [0, 1, 2],
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
                                                columns: [0, 1, 2],
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
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">Add New Type</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#offcanvasCreateSurveyType'
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
    const createSurveyTypeForm = document.getElementById('createSurveyType');

    if (!createSurveyTypeForm) return;

    const fv = FormValidation.formValidation(createSurveyTypeForm, {
        fields: {
            surveyTypeName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter name '
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

function handleFormSubmit(fv) {
    fv.on('core.form.valid', function () {

        const name = document.getElementById('add-name').value;
        const description = document.getElementById('add-description')?.value || "";
        const userName = window.getUserName();
        const formData = new FormData();
        formData.append("name", name);
        if (description.trim() !== "") {  // boş değilse ekle
            formData.append("description", description);
        }
        formData.append("createdBy", userName);
        formData.append("isActive", true);



        
       
        //table.ajax.reload();

        fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveyType/CreateSurveyType`, {
            method: 'POST',
            body: formData
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                document.getElementById('add-description').value = '';
                const table = $('.survey-type-table').DataTable();
                table.ajax.reload();
                showToast('The record has been added successfully.', "success");
            })
            .catch(error => {
                console.error(error);
                alert('Kayıt sırasında bir hata oluştu.');
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

        const deletedData = new FormData();
        deletedData.append("id", recordIdToDelete);
        deletedData.append("modifiedBy", userName);

        try {
            const response = await fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveyType/DeleteSurveyType`, {
                method: 'POST',
                body: deletedData
            });

            const result = await response.json();

            if (result.isSuccessful || result.data === true) {
                const table = $('.survey-type-table').DataTable();
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

function initEdit() {
    $(document).on('click', '.edit-record', function () {
        const id = $(this).data('id');
        const name = $(this).data('name');
        const description = $(this).data('description');
        const status = $(this).data('status');

        $('#update-name').val(name);
        $('#update-description').val(description);
        $('#updateSurveyType').attr('data-survey-type-id', id);

        const offcanvasEl = document.getElementById('offcanvasUpdateSurveyType');
        const bsOffcanvas = new bootstrap.Offcanvas(offcanvasEl);
        bsOffcanvas.show();
    });
}

function initUpdateFormValidation() {
    const updateForm = document.getElementById('updateSurveyType');

    if (!updateForm) return;

    const fv = FormValidation.formValidation(updateForm, {
        fields: {
            surveyTypeName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter name'
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

    handleUpdateFormSubmit(fv);
}


function handleUpdateFormSubmit(fv) {
    fv.on('core.form.valid', function () {
        const form = document.getElementById('updateSurveyType');
        const surveyTypeId = $('#updateSurveyType').attr('data-survey-type-id');
        const userName = window.getUserName();
        const name = document.getElementById('update-name').value;
        const description = document.getElementById('update-description')?.value || "";
        const updatedData = new FormData();
        updatedData.append("id", surveyTypeId);
        updatedData.append("name", name);
        if (description.trim() !== "") {  // boş değilse ekle
            updatedData.append("description", description);
        }
        updatedData.append("createdBy", userName);
        updatedData.append("isActive", true);


       

        fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveyType/UpdateSurveyType`, {
            method: 'POST',
            body: updatedData
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                /*if (data.isSuccessful) {*/
                    const table = $('.survey-type-table').DataTable();
                    table.ajax.reload();

                    const offcanvasEl = document.getElementById('offcanvasUpdateSurveyType');
                    const bsOffcanvas = bootstrap.Offcanvas.getInstance(offcanvasEl);
                    bsOffcanvas.hide();

                    showToast('The record has been updated successfully.', "success");
                //} else {
                //    const errorMessage = data.errors?.join('<br>') || 'Güncelleme sırasında bir hata oluştu.';
                //    showToast(errorMessage, "error");
                //}
            })
            .catch(error => {
                console.error(error);

                showToast('Sunucuya bağlanırken bir hata oluştu.', "error");
            });
    });
}

function initActiveDisableSurveyType() {
    $(document).on('click', '.activate-disabled-record', function () {

        const surveyTypeId = $(this).data('id');
        const status = $(this).data('status');
        const userName = window.getUserName();
        const name = $(this).data('name');
        const description = $(this).data('description')?.value || "";

        let updateStatus = status ? false : true;


        const updatedData = new FormData();
        updatedData.append("id", surveyTypeId);
        updatedData.append("name", name);
        if (description.trim() !== "") {  // boş değilse ekle
            updatedData.append("description", description);
        }
        updatedData.append("modifiedBy", userName);
        updatedData.append("isActive", updateStatus);


       

        fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveyType/UpdateSurveyType`, {
            method: 'POST',
            body: updatedData
        })
            .then(response => response.json())
            .then(data => {
                
                /*if (data.isSuccessful) {*/
                    const table = $('.survey-type-table').DataTable();
                    table.ajax.reload();

                    showToast('The record has been changed status successfully.', "success");
                //} else {
                //    const errorMessage = data.errors?.join('<br>') || 'Güncelleme sırasında bir hata oluştu.';
                //    showToast(errorMessage, "error");
                //}
            })
            .catch(error => {
                console.error(error);

                showToast('Sunucuya bağlanırken bir hata oluştu.', "error");
            });



    });
}



