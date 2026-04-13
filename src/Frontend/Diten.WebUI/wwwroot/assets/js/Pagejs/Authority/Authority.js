'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
document.addEventListener('DOMContentLoaded', function () {

    const lang = localStorage.getItem('language') || 'en';

    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["SearchAuthority"] || "Search Authority";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initAuthorityDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Dil dosyası yüklenemedi:', error);
            initAuthorityDataTable("Search Authority", data); // fallback
        });

    modifyDataTableLayout();
    const url = `${protocol}//${domain}:${port}/services/PvTenant/Tenant/GetCountriesByTenantId`;
    fetchCountries(url, "add-ddlCountry");
    fetchCountries(url, "update-ddlCountry");

    initializeFormValidation();
    bindDeleteRecordEvent();
    initEditAuthority();
    initializeUpdateFormValidation();
});

function initAuthorityDataTable(placeholderText, lanData) {

    const dt_authority_table = document.querySelector('.authority-table');
    if (dt_authority_table) {
        const dt_company = new DataTable(dt_authority_table, {
            ajax: {
                url: `${protocol}//${domain}:${port}/services/PvTenant/Authority/GetAuthoritiesByTenantId`,
                method: 'GET',
                dataSrc: 'data',
                error: function (jqxhr, textStatus, errorThrown) {
                    // Bu callback sadece DataTable özelinde hata olursa çalışır
                    console.error("DataTable Hatası:", jqxhr.status, errorThrown);
                    if (jqxhr.status !== 200) {
                        window.location.href = '/pages-misc-error.html?code=' + jqxhr.status; // Hata kodu ile yönlendirme
                    }
                }
            },
            columns: [
                { data: 'id' },
                //{ data: 'id', orderable: false, render: DataTable.render.select() },
                //{ data: 'id' },
                { data: 'countryName' },
                { data: 'name' },
                { data: 'address' },
                { data: 'website' },
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
                                <a href="javascript:;" class="btn btn-icon edit-record" data-id="${full.id}" data-name="${full.name}" data-country="${full.countryId}" data-address="${full.address}"  data-website="${full.website}" >
                                    <i class="icon-base bx bx-edit-alt icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon delete-record " data-id="${full.id}">
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
            order: [[2, 'asc']],
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
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block" data-i18n="AddNewAuthorities">Add New Authorities</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#AddAuthority'
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

function initializeFormValidation() {
    const addAuthorityForm = document.getElementById('addAuthorityForm');

    if (!addAuthorityForm) return;

    const fv = FormValidation.formValidation(addAuthorityForm, {
        fields: {
            authorityName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter name '
                    }
                }
            },
            authorityCountry : {
                validators: {
                    notEmpty: {
                        message: 'Please select a country '
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
        const name = document.getElementById('add-authority-name').value;
        const country = document.getElementById('add-ddlCountry').value;
        const address = document.getElementById('add-txt-address').value;
        const website = document.getElementById('add-txt-website').value;
        const userName = window.getUserName();
        const formData = {
            name: name,
            countryId: country,
            tenantId:"",
            address: address,
            website: website,
            createdBy: userName
        };

        fetch(`${protocol}//${domain}:${port}/services/PvTenant/Authority/CreateAuthority`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                addAuthorityForm.reset();
                if (data.isSuccessful) {

                    const table = $('.authority-table').DataTable();
                    table.ajax.reload();

                }
                else {
                    const errorMessage = data.errors?.join('<br>') || 'Güncelleme sırasında bir hata oluştu.';
                    showToast(errorMessage, "error");
                }

                
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
            const response = await fetch(`${protocol}//${domain}:${port}/services/PvTenant/Authority/DeleteTenantAuthority`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.authority-table').DataTable();
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

function initEditAuthority() {
    $(document).on('click', '.edit-record', async function () {

        const id = $(this).data('id');
        const name = $(this).data('name');
        const country = $(this).data('country');
        const address = $(this).data('address');
        const webSite = $(this).data('website');

        

        $('#update-authority-name').val(name);
        $('#update-ddlCountry').val(country).trigger('change');
        $('#update-txt-address').val(address);
        $('#update-txt-website').val(webSite);
        $('#updateAuthorityForm').attr('data-id', id);
        const offcanvasEl = document.getElementById('UpdateAuthority');
        const bsOffcanvas = new bootstrap.Offcanvas(offcanvasEl);
        bsOffcanvas.show();
    });
}

function initializeUpdateFormValidation() {
    const updateForm = document.getElementById('updateAuthorityForm');

    if (!updateForm) return;

    const fv = FormValidation.formValidation(updateForm, {
        fields: {
            updateAuthorityName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter a name'
                    }
                }
            },
            updateAuthorityCountry: {
                validators: {
                    notEmpty: {
                        message: 'Please select a country'
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
        const form = document.getElementById('updateAuthorityForm');
        const Id = $('#updateAuthorityForm').attr('data-id');
        const name = document.getElementById('update-authority-name').value;
        const country = document.getElementById('update-ddlCountry').value;
        const address = document.getElementById('update-txt-address').value;
        const website = document.getElementById('update-txt-website').value;
        const userName = window.getUserName();

        const updatedData = {
            id: Id,
            name: name,
            tenantId:"",
            countryId: country,
            address: address,
            website: website,
            modifiedBy: userName
        };

        fetch(`${protocol}//${domain}:${port}/services/PvTenant/Authority/UpdateTenantAuthority`, {
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


                    const table = $('.authority-table').DataTable();
                    table.ajax.reload();


                    const offcanvasEl = document.getElementById('UpdateAuthority');
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

function decodeJWT(token) {

    const base64Url = token.split('.')[1];  // Token'ın ikinci kısmı payload'dır
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');  // Base64 formatını düzelt
    const jsonPayload = decodeURIComponent(atob(base64).split('').map(function (c) {
        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
    }).join(''));
    return JSON.parse(jsonPayload);
}