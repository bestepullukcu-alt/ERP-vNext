'use strict';
const port2 = protocol === 'https:' ? '5055' : '5050';
const port3 = protocol === 'https:' ? '5060' : '5053';
document.addEventListener('DOMContentLoaded', function () {
    let borderColor, bodyBg, headingColor;
    borderColor = config.colors.borderColor;
    bodyBg = config.colors.bodyBg;
    headingColor = config.colors.headingColor;


    const lang = localStorage.getItem('language') || 'en';

    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["SearchUser"] || "Search User";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initUserDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Dil dosyası yüklenemedi:', error);
            initUserDataTable("Search User", data); // fallback
        });
    modifyDataTableLayout();

    const dropdownUrl = `${window.ApiBaseUrl}/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId`;
    const selectUrl = `${window.ApiBaseUrl}/services/PvTenant/Role/GetActiveRolesByTenantId`;


    fetchDropdownlist(dropdownUrl, "ddlUserCompany");

    loadSelectPickerOptions(selectUrl, "ddlRoles", "id", "name");

    initializeFormValidation();
    initActiveDisableUser();
    bindDeleteRecordEvent();
});

function initUserDataTable(placeholderText, lanData) {

    const dt_user_table = document.querySelector('.user-table');
    if (dt_user_table) {

        const companyId = document.getElementById('ddlCompany').value;       
        const dt_user = new DataTable(dt_user_table, {
            ajax: {
                url: `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByCompanyId`,
                method: 'GET',
                data: function (d) {
                   return d; 
                },
                dataSrc: 'data',
                beforeSend: function (xhr, settings) {
                    
                    if (companyId) {
                        alert(companyId);
                        settings.url += `?CompanyId=${companyId}`;
                    }
                }
            },
            columns: [
                { data: 'id' },
                { data: 'fullName' },
                { data: 'userRoles' },
                { data: 'companyName' },
                { data: 'activeStr' },
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
                    targets: 1,
                    responsivePriority: 3,
                    render: function (data, type, full, meta) {
                        var name = full['fullName'];
                        var email = full['email'];
                        var image = full['image'];
                        var output;

                        if (image) {
                            // For Avatar image
                            output = '<img src="' + assetsPath + 'img/avatars/' + image + '" alt="Avatar" class="rounded-circle">';
                        } else {
                            // For Avatar badge
                            var stateNum = Math.floor(Math.random() * 6);
                            var states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
                            var state = states[stateNum];
                            var initials = (name.split(' ').map(word => word[0]).join('')).toUpperCase();
                            output = '<span class="avatar-initial rounded-circle bg-label-' + state + '">' + initials + '</span>';
                        }

                        // Creates full output for row
                        var row_output =
                            '<div class="d-flex justify-content-start align-items-center user-name">' +
                            '<div class="avatar-wrapper">' +
                            '<div class="avatar avatar-sm me-4">' +
                            output +
                            '</div>' +
                            '</div>' +
                            '<div class="d-flex flex-column">' +
                            '<a href="#" class="text-heading text-truncate"><span class="fw-medium">' +
                            name +
                            '</span></a>' +
                            '<small>' +
                            email +
                            '</small>' +
                            '</div>' +
                            '</div>';
                        return row_output;
                    }
                },

                {
                    targets: 2, // isActiveStr sütunu
                    render: function (data, type, full) {
                        if (!data || data.length === 0) return '-';

                        return data.map(x => {
                            const icon = x.roleName === "Admin"
                                ? '<i class="icon-base bx bx-crown text-primary me-2"></i>'
                                : '<i class="icon-base bx bx-user text-success me-2"></i>';
                            return icon + x.roleName;
                        }).join('<br>'); // Her rolü alt alta göstermek istersen <br> koyabilirsin. Virgül için ',' kullan.
                    }
                },
                {
                    targets: 4, // isActiveStr sütunu
                    render: function (data, type, full, meta) {

                        return data === 'Active'
                            ? `<span class="badge bg-label-success text-capitalized">${data}</span>`
                            : `<span class="badge bg-label-danger text-capitalized">${data}</span>`;
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
                            <a href="javascript:;" class="btn btn-icon disabled opacity-50">
                            <i class="icon-base bx bx-show icon-md"></i>
                            </a>
                                <a href="javascript:;" class="btn btn-icon activate-disabled-record" data-id="${full.id}" data-status="${full.activeStr}">
                                    <i class="icon-base bx bx-power-off icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon edit-record disabled opacity-50" data-id="${full.id}" data-name="${full.fullName}">
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
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block" data-i18n="AddUser">Add New User</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#AddUser'
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
                            return 'Details of ' + data['fullName'];
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

async function fetchDropdownlist(apiUrl, selectElementId) {
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
        const defaultOption = new Option("Select a value", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(company => {
            const value = company.id ?? company.companyName;
            const option = new Option(company.companyName, value, false, false);
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

async function loadSelectPickerOptions(apiUrl, selectId, valueField, textField) {

    const response = await fetch(apiUrl);
    if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
    }
    const result = await response.json();
    const $select = $(`#${selectId}`);
    $select.empty();

    result.data.forEach(item => {
        const value = item[valueField];
        const text = item[textField];
        $select.append(new Option(text, value));
    });

    $select.selectpicker('refresh');

}

function initializeFormValidation() {
    const addUserForm = document.getElementById('addNewUserForm');

    if (!addUserForm) return;

    const fv = FormValidation.formValidation(addUserForm, {
        fields: {
            userFullname: {
                validators: {
                    notEmpty: {
                        message: 'Please enter fullname'
                    }
                }
            },
            userEmail: {
                validators: {
                    notEmpty: {
                        message: 'Please enter email'
                    }
                }
            },
            ddlRoles: {
                validators: {
                    notEmpty: {
                        message: 'Please select a role'
                    }
                }
            },
            userUsername: {
                validators: {
                    notEmpty: {
                        message: 'Please enter username'
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

        const fullName = document.getElementById('add-user-fullname').value;
        const createUserName = document.getElementById('add-user-username').value;
        const email = document.getElementById('add-user-email').value;
        const contact = document.getElementById('add-user-contact').value;
        const company = document.getElementById('ddlUserCompany').value;
        const roles = document.getElementById('ddlRoles');
        const selectedRoles = Array.from(roles.selectedOptions).map(option => option.value);
        const userName = window.getUserName();


        const formData = {
            fullName: fullName,
            userName: createUserName,
            email: email,
            phone: contact,
            image: "",
            roleIds: selectedRoles,
            companyId: company,
            operatingCountryIds: [],
            marketingAuthorizationIds: [],
            createdBy: userName
        };
        fetch(`${protocol}//${domain}:${port2}/api/PvUser/User/CreateUser`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                

                // Eğer modal içinde ise, modal'ı kapat:
                // bootstrap.Modal.getInstance(document.getElementById('yourModalId')).hide();

                const table = $('.user-table').DataTable();
                table.ajax.reload();
            })
            .catch(error => {
                console.error(error);
                showToast('Kayıt sırasında bir hata oluştu.', "error");

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

function initActiveDisableUser() {
    $(document).on('click', '.activate-disabled-record', function () {

        const userId = $(this).data('id');
        const status = $(this).data('status');
        const userName = window.getUserName();

        const statusData = {
            id: userId,
            isActive: status == 'Active' ? false : true,
            modifiedBy: userName
        };
        fetch(`${protocol}//${domain}:${port2}/api/PvUser/User/activate-disable`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(statusData)
        })
            .then(response => response.json())
            .then(data => {
                if (data.errors == null) {
                    const table = $('.user-table').DataTable();
                    table.ajax.reload();
                    showToast('The record has been changed status successfully.', "success");
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
            const response = await fetch(`${protocol}//${domain}:${port2}/api/PvUser/User/DeleteUser`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.user-table').DataTable();
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

