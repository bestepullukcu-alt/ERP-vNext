'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
document.addEventListener('DOMContentLoaded', function () {
    initActiveIngredientDataTable();
    bindDeleteRecordEvent();
    modifyDataTableLayout();
    initializeFormValidation();
    initEditActiveIngredient();
    initializeUpdateFormValidation();
});

function initActiveIngredientDataTable() {
    const dt_activeIngredient_table = document.querySelector('.activeIngredient-table');
    if (dt_activeIngredient_table) {
        const dt_activeIngredient = new DataTable(dt_activeIngredient_table, {
            ajax: {
                url: `${protocol}//${domain}:${port}/services/PvTenant/TenantActiveIngredient/GetActiveIngredientsByTenantId`,
                method: 'GET',
                dataSrc: 'data'
            },
            columns: [
                { data: 'id' },
                //{ data: 'id', orderable: false, render: DataTable.render.select() },
                //{ data: 'id' },
                { data: 'name' },                
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
                    targets: -1,
                    title: 'Actions',
                    searchable: false,
                    orderable: false,
                    render: (data, type, full) => {

                        return `
                            <div class="d-flex align-items-center">                               
                                <a href="javascript:;" class="btn btn-icon edit-record" data-id="${full.id}" data-name="${full.name}">
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
                                placeholder: 'Search Active Ingredient',
                                text: '_INPUT_'
                            }
                        },
                        {
                            buttons: [
                                {
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">Add New Ingredient</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#AddActiveIngredient'
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
            const response = await fetch(`${protocol}//${domain}:${port}/services/PvTenant/TenantActiveIngredient/DeleteTenantActiveIngredient`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.activeIngredient-table').DataTable();
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

function initializeFormValidation() {
    const addActiveIngredientForm = document.getElementById('addActiveIngredientForm');

    if (!addActiveIngredientForm) return;

    const fv = FormValidation.formValidation(addActiveIngredientForm, {
        fields: {
            activeIngredientName: {
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

        const name = document.getElementById('add-active-ingredient').value;
        const userName = window.getUserName();

        const formData = {
            name: name,
            tenantId:"",
            createdBy: userName
        };
        fetch(`${protocol}//${domain}:${port}/services/PvTenant/TenantActiveIngredient/CreateTenantActiveIngredient`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                addActiveIngredientForm.reset();

                // Eğer modal içinde ise, modal'ı kapat:
                // bootstrap.Modal.getInstance(document.getElementById('yourModalId')).hide();

                const table = $('.activeIngredient-table').DataTable();
                table.ajax.reload();
            })
            .catch(error => {
                console.error(error);
                showToast('Kayıt sırasında bir hata oluştu.', "error");

            });
    });
}

function initEditActiveIngredient() {
    $(document).on('click', '.edit-record', function () {
        const id = $(this).data('id');
        const name = $(this).data('name');
        

        $('#update-active-ingredient').val(name);        
        $('#updateActiveIngredientForm').attr('data-id', id);

        const offcanvasEl = document.getElementById('UpdateActiveIngredient');
        const bsOffcanvas = new bootstrap.Offcanvas(offcanvasEl);
        bsOffcanvas.show();
    });
}

function initializeUpdateFormValidation() {
    const updateForm = document.getElementById('updateActiveIngredientForm');

    if (!updateForm) return;

    const fv = FormValidation.formValidation(updateForm, {
        fields: {
            updateActiveIngredientName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter a name'
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
        const form = document.getElementById('updateActiveIngredientForm');
        const activeIngredientId = $('#updateActiveIngredientForm').attr('data-id');
        const activeIngredientName = document.getElementById('update-active-ingredient').value;
        const userName = window.getUserName();

        const updatedData = {
            id: activeIngredientId,
            name: activeIngredientName,
            tenantId:"",
            modifiedBy: userName
        };

        fetch(`${protocol}//${domain}:${port}/services/PvTenant/TenantActiveIngredient/UpdateTenantActiveIngredient`, {
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
                if (data.isSuccessful) {


                    const table = $('.activeIngredient-table').DataTable();
                    table.ajax.reload();


                    const offcanvasEl = document.getElementById('UpdateActiveIngredient');
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

