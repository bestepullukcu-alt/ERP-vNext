'use strict';
document.addEventListener('DOMContentLoaded', function () {

    initPharmaceuticalFormDataTable();
    bindDeleteRecordEvent();
    modifyDataTableLayout();
    const url = `${window.ApiBaseUrl}/services/PvTenant/TenantPharmaceuticalForm/GetPharmaceuticalFormTypes`;

    fetchTypes(url, "add-pharmaceutical-form-type");
    fetchTypes(url, "update-pharmaceutical-form-type");
    initializeFormValidation();
    initEditPharmaceuticalForm();
    initializeUpdateFormValidation();
});

function initPharmaceuticalFormDataTable() {
    const dt_pharmaceutical_form_table = document.querySelector('.pharmaceutical-form-table');
    if (dt_pharmaceutical_form_table) {
        const dt_pharmaceutical_form = new DataTable(dt_pharmaceutical_form_table, {
            ajax: {
                url: `${window.ApiBaseUrl}/services/PvTenant/TenantPharmaceuticalForm/GetPharmaceuticalFormByTenantId`,
                method: 'GET',
                dataSrc: 'data'
            },
            columns: [
                { data: 'id' },
                //{ data: 'id', orderable: false, render: DataTable.render.select() },
                //{ data: 'id' },
                { data: 'name' },
                { data: 'abbrevation' },
                { data: 'typeName' },
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
                    // Invoice Status with tooltip
                    targets: 3,
                    render: function (data, type, full) {
                        const typeId = full.typeId;
                        if (typeId===1) {

                            return `<span class="badge bg-info me-1">${full.typeName}</span>`;
                        }
                        return full.typeName;
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
                                <a href="javascript:;" class="btn btn-icon edit-record" data-id="${full.id}" data-name="${full.name}" data-type-id="${full.typeId}" data-abbrevation="${full.abbrevation}">
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
                                text: 'Show_MENU_'
                            },
                        }
                    ]
                },
                topEnd: {
                    rowClass: 'row mx-3 justify-content-between',
                    features: [
                        {
                            search: {
                                placeholder: 'Search Pharmaceutical Form',
                                text: '_INPUT_'
                            }
                        },
                        {
                            buttons: [
                                {
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">Add New Form</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#AddPharmaceuticalForm'
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
                }
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
        const userName = window.getUserName();
        try {

            const response = await fetch(`${window.ApiBaseUrl}/services/PvTenant/TenantPharmaceuticalForm/DeleteTenantPharmaceuticalForm`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.pharmaceutical-form-table').DataTable();
                table.ajax.reload();

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

function modifyDataTableLayout() {
    setTimeout(() => {
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
    }, 100);
}

async function fetchTypes(apiUrl, selectElementId) {
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
        const defaultOption = new Option("Select a type", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(formType => {
            const value = formType.id ?? formType.name;
            const option = new Option(formType.name, value, false, false);
            selectElement.appendChild(option);
        });

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }
    } catch (error) {
        console.error("Typelar alınırken hata oluştu:", error);
    }
}

function initializeFormValidation() {
    const addPharmaceuticalFormForm = document.getElementById('addPharmaceuticalFormForm');

    if (!addPharmaceuticalFormForm) return;

    const fv = FormValidation.formValidation(addPharmaceuticalFormForm, {
        fields: {
            pharmaceuticalFormName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter name '
                    }
                }
            },
            pharmaceuticalFormAbbrevation: {
                validators: {
                    notEmpty: {
                        message: 'Please enter abbrevaiton '
                    }
                }
            },
            pharmaceuticalFormType: {
                validators: {
                    notEmpty: {
                        message: 'Please select a type '
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

        const name = document.getElementById('add-pharmaceutical-form-name').value;
        const abb = document.getElementById('add-pharmaceutical-form-abbrevation').value;
        const type = document.getElementById('add-pharmaceutical-form-type').value;
        const userName = window.getUserName();

        const formData = {
            name: name,
            abbrevation: abb,
            typeId: type,
            tenantId:"",
            createdBy: userName
        };
        fetch(`${window.ApiBaseUrl}/services/PvTenant/TenantPharmaceuticalForm/CreateTenantPharmaceuticalForm`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                addPharmaceuticalFormForm.reset();

                // Eğer modal içinde ise, modal'ı kapat:
                // bootstrap.Modal.getInstance(document.getElementById('yourModalId')).hide();

                const table = $('.pharmaceutical-form-table').DataTable();
                table.ajax.reload();
            })
            .catch(error => {
                console.error(error);
                showToast('Kayıt sırasında bir hata oluştu.', "error");

            });
    });
}

function initEditPharmaceuticalForm() {
    $(document).on('click', '.edit-record', function () {
        const id = $(this).data('id');
        const name = $(this).data('name');
        const abbrevation = $(this).data('abbrevation');
        const typeId = $(this).data('type-id');

        $('#update-pharmaceutical-form-name').val(name);
        $('#update-pharmaceutical-form-abbrevation').val(abbrevation);
        $('#update-pharmaceutical-form-type').val(typeId).trigger('change');        
        $('#updatePharmaceuticalFormForm').attr('data-id', id);

        const offcanvasEl = document.getElementById('UpdatePharmaceuticalForm');
        const bsOffcanvas = new bootstrap.Offcanvas(offcanvasEl);
        bsOffcanvas.show();
    });
}

function initializeUpdateFormValidation() {
    const updateForm = document.getElementById('updatePharmaceuticalFormForm');

    if (!updateForm) return;

    const fv = FormValidation.formValidation(updateForm, {
        fields: {
            updatePharmaceuticalFormName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter a name'
                    }
                }
            },
            updatePharmaceuticalFormAbbrevation: {
                validators: {
                    notEmpty: {
                        message: 'Please enter a abbrevation'
                    }
                }
            },
            updatePharmaceuticalFormType: {
                validators: {
                    notEmpty: {
                        message: 'Please select a type'
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
        const form = document.getElementById('updatePharmaceuticalFormForm');
        const pharmaceuticalFormId = $('#updatePharmaceuticalFormForm').attr('data-id');
        const name = document.getElementById('update-pharmaceutical-form-name').value;
        const abb = document.getElementById('update-pharmaceutical-form-abbrevation').value;
        const type = document.getElementById('update-pharmaceutical-form-type').value;
        const userName = window.getUserName();

        const updatedData = {
            id: pharmaceuticalFormId,
            name: name,
            abbrevation: abb,
            typeId: type,
            tenantId:"",
            modifiedBy: userName
        };

        fetch(`${window.ApiBaseUrl}/services/PvTenant/TenantPharmaceuticalForm/UpdateTenantPharmaceuticalForm`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(updatedData)
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                //addCompanyForm.reset();
                const isSuccess = data.errors === null;
                if (isSuccess) {


                    const table = $('.pharmaceutical-form-table').DataTable();
                    table.ajax.reload();


                    const offcanvasEl = document.getElementById('UpdatePharmaceuticalForm');
                    const bsOffcanvas = bootstrap.Offcanvas.getInstance(offcanvasEl);
                    bsOffcanvas.hide();

                    showToast('The record has been added successfully.', "success");
                } else {
                    const errorMessage = data.errors?.join('<br>') || 'Güncelleme sırasında bir hata oluştu.';
                    showToast(errorMessage, "error");
                }
            })
            .catch(error => {
                console.error(error);

                showToast('Sunucuya bağlanırken bir hata oluştu.', "error");
            });
    });
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

