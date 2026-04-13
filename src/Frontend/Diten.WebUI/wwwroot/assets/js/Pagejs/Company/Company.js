'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
document.addEventListener('DOMContentLoaded', function () {

    const lang = localStorage.getItem('language') || 'en';

    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["SearchCompany"] || "Search Company";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initCompanyDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Dil dosyası yüklenemedi:', error);
            initCompanyDataTable("Search Company",data); // fallback
        });


    modifyDataTableLayout();
    bindDeleteRecordEvent();
    const countryUrl = `${protocol}//${domain}:${port}/services/PvTenant/Tenant/GetCountriesByTenantId`;
    const parentCompanyUrl = `${protocol}//${domain}:${port}/services/PvOrganization/OrganizationControlller/GetParentCompanies`;

    fetchCountries(countryUrl, "add-company-country");
    fetchCountries(countryUrl, "update-company-country");
    //loadSelectPickerOptions("http://localhost:5000/services/PvTenant/Tenant/GetCountriesByTenantId", "ddlOperatingCountries", "id", "name");
    //loadSelectPickerOptions("http://localhost:5000/services/PvTenant/Tenant/GetCountriesByTenantId", "updateddlOperatingCountries", "id", "name");
    fetchParentCompany(parentCompanyUrl, "add-ParentCompany", null,null);
    initializeFormValidation();
    initEditCompany();
    initializeUpdateFormValidation();
});


function initCompanyDataTable(placeholderText, lanData) {

    const dt_company_table = document.querySelector('.company-table');
    if (dt_company_table) {
        const dt_company = new DataTable(dt_company_table, {
            ajax: {
                url: `${protocol}//${domain}:${port}/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId`,
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
                { data: 'companyName' },
                { data: 'countryName' },
                { data: 'typeStrList' }, 
                { data: 'parentCompanyName' }, 
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
                    render: function (data, type, row) {
                        if (Array.isArray(data)) {
                            return data.map(type => `<span class="badge bg-primary me-1">${type}</span>`).join('');
                        }
                        return '';
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    searchable: false,
                    orderable: false,
                    render: (data, type, full) => {
                        const isDisabled = full.isTenant;

                        const disabledClass = isDisabled ? 'disabled opacity-50' : '';
                        return `
                            <div class="d-flex align-items-center">
                                <a href="javascript:;" class="btn btn-icon activate-disabled-record" data-id="${full.id}">
                                    <i class="icon-base bx bx-show icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon ${disabledClass} edit-record" data-id="${full.id}" data-name="${full.companyName}" data-country="${full.countryId}" data-abbrevation="${full.abbrevation}" data-address="${full.address}" data-phone="${full.phone}" data-email="${full.email}" data-webSite="${full.webSite}" data-operatingcountries="${full.operatingCountries.join(',')}" data-isgroup="${full.isGroup}" data-parentcompanyid="${full.parentCompanyId}">
                                    <i class="icon-base bx bx-edit-alt icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon delete-record ${disabledClass}" data-id="${full.id}">
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
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block" data-i18n="AddCompany">Add Company</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#AddCompany'
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
                            return 'Details of ' + data['companyName'];
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
            const response = await fetch(`${protocol}//${domain}:${port}/services/PvOrganization/OrganizationControlller/DeleteOrganization`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.company-table').DataTable();
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

async function fetchParentCompany(apiUrl, selectElementId, selectedCompanyId, excludeCompanyId) {
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
        const defaultOption = new Option("Select a company", "", false, false);
        selectElement.appendChild(defaultOption);

        let companyList = data.data || [];
        if (excludeCompanyId != null && excludeCompanyId !== "") {
            companyList = companyList.filter(company => company.id !== excludeCompanyId);
        }

        // Seçilecek ID belirleniyor
        let autoSelectId = null;
        if (selectedCompanyId != null && selectedCompanyId !== "") {
            autoSelectId = selectedCompanyId;
        } else if (companyList.length === 1 && (selectedCompanyId == null || selectedCompanyId == "") && (excludeCompanyId == null || excludeCompanyId=="")) {
            autoSelectId = companyList[0].id;
        }

        companyList.forEach(company => {
            const value = company.id ?? company.companyName;
            const isSelected = autoSelectId != null && value === autoSelectId;

            const option = new Option(company.companyName, value, isSelected, isSelected);
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
    const addCompanyForm = document.getElementById('addCompanyForm');

    if (!addCompanyForm) return;

    const fv = FormValidation.formValidation(addCompanyForm, {
        fields: {
            companyName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter name '
                    }
                }
            },
            companyCountry: {
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

        const companyName = document.getElementById('add-company-name').value;
        const companyAbb = document.getElementById('add-company-abbrevation').value;
        const companyCountry = document.getElementById('add-company-country').value;
        const companyAddress = document.getElementById('add-company-address').value;
        const companyPhone = document.getElementById('add-company-phone').value;
        const companyEmail = document.getElementById('add-company-email').value;
        const companyWebsite = document.getElementById('add-company-website').value;
        const parentCompanyId = document.getElementById('add-ParentCompany').value;
        const isGroup = document.getElementById('chcIsGroup').checked;
        const userName = window.getUserName();


        const formData = {
            companyName: companyName,
            abbrevation: companyAbb,
            countryId: companyCountry,
            address: companyAddress,
            phone: companyPhone,
            email: companyEmail,
            webSite: companyWebsite,
            logo: "",
            isTenant: false,
            isLocalCompany: false,
            taxNumber: "",
            vatNumber: "",
            signalDetectionParticipation: false,
            operatingCountries: [],
            emaCompanyNumber: "",
            fdaRegistrationNumber: "",
            isEudraVigilanceRegistered: false,
            isGVPCompliance: false,
            createdBy: userName,
            isGroup: isGroup,
            parentCompanyId: parentCompanyId
        };
        fetch(`${protocol}//${domain}:${port}/services/PvOrganization/OrganizationControlller/CreateOrganization`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                addCompanyForm.reset();

                // Eğer modal içinde ise, modal'ı kapat:
                // bootstrap.Modal.getInstance(document.getElementById('yourModalId')).hide();

                const table = $('.company-table').DataTable();
                table.ajax.reload();
            })
            .catch(error => {
                console.error(error);
                showToast('Kayıt sırasında bir hata oluştu.', "error");

            });
    });
}

function initEditCompany() {
    $(document).on('click', '.edit-record', async function () {       
        const id = $(this).data('id');
        const name = $(this).data('name');
        const abbrevation = $(this).data('abbrevation');
        const country = $(this).data('country');
        const address = $(this).data('address');
        const phone = $(this).data('phone');
        const email = $(this).data('email');
        const webSite = $(this).data('webSite');
        const isGroup = $(this).data('isgroup');
        const parentCompanyId = $(this).data('parentcompanyid');
        const parentCompanyUrl = `${protocol}//${domain}:${port}/services/PvOrganization/OrganizationControlller/GetParentCompanies`;
        fetchParentCompany(parentCompanyUrl, "update-ParentCompany", parentCompanyId, id);


        //const operatingCountriesRaw = $(this).data('operatingcountries'); // örn: "1,3,7"
        //const selectedCountries = operatingCountriesRaw?.toString().split(',').map(x => x.trim()) ?? [];

        document.getElementById('updateChcIsGroup').checked = isGroup;
        $('#update-company-name').val(name);
        $('#update-company-abbrevation').val(abbrevation);
        $('#update-company-country').val(country).trigger('change');
        $('#update-company-address').val(address);
        $('#update-company-phone').val(phone);
        $('#update-company-email').val(email);
        $('#update-company-website').val(webSite);
        $('#updateCompanyForm').attr('data-id', id);
        //$("#updateddlOperatingCountries").selectpicker("destroy");
        //$("#updateddlOperatingCountries")
        //    .addClass("w-100")
        //    .addClass("selectpicker"); // çok önemli
        //     // çok önemli
        //    // gerekiyorsa tekrar ekle

        //$("#updateddlOperatingCountries").val(selectedCountries);
        //$("#updateddlOperatingCountries").selectpicker();
       

        const offcanvasEl = document.getElementById('UpdateCompany');
        const bsOffcanvas = new bootstrap.Offcanvas(offcanvasEl);
        bsOffcanvas.show();
    });
}

function initializeUpdateFormValidation() {
    const updateForm = document.getElementById('updateCompanyForm');

    if (!updateForm) return;

    const fv = FormValidation.formValidation(updateForm, {
        fields: {
            updateCompanyName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter a name'
                    }
                }
            },
            updateCompanyCountry: {
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
        const form = document.getElementById('updateCompanyForm');
        const companyId = $('#updateCompanyForm').attr('data-id');
        const companyName = document.getElementById('update-company-name').value;
        const companyAbb = document.getElementById('update-company-abbrevation').value;
        const companyCountry = document.getElementById('update-company-country').value;
        const companyAddress = document.getElementById('update-company-address').value;
        const companyPhone = document.getElementById('update-company-phone').value;
        const companyEmail = document.getElementById('update-company-email').value;
        const companyWebsite = document.getElementById('update-company-website').value;
        const parentCompanyId = document.getElementById('update-ParentCompany').value;
        const isGroup = document.getElementById('updateChcIsGroup').checked;
        //const operatingCountriesSelect = document.getElementById('updateddlOperatingCountries');
        //const selectedOperatingCountries = Array.from(operatingCountriesSelect.selectedOptions).map(option => option.value);
        const userName = window.getUserName();


        const updatedData = {
            id: companyId,
            companyName: companyName,
            abbrevation: companyAbb,
            countryId: companyCountry,
            address: companyAddress,
            phone: companyPhone,
            email: companyEmail,
            webSite: companyWebsite,
            logo: "",
            isTenant: false,
            isLocalCompany: false,
            taxNumber: "",
            vatNumber: "",
            signalDetectionParticipation: false,
            operatingCountries: [],
            emaCompanyNumber: "",
            fdaRegistrationNumber: "",
            isEudraVigilanceRegistered: false,
            isGVPCompliance: false,
            isGroup: isGroup,
            parentCompanyId: parentCompanyId,
            modifiedBy: userName
        };
        fetch(`${protocol}//${domain}:${port}/services/PvOrganization/OrganizationControlller/UpdateOrganization`, {
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
                   

                    const table = $('.company-table').DataTable();
                    table.ajax.reload();


                    const offcanvasEl = document.getElementById('UpdateCompany');
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

async function loadSelectPickerOptions(apiUrl, selectId, valueField, textField) {
    const response = await fetch(apiUrl);
    if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
    }
    const result = await response.json();
    const $select = $(`#${selectId}`);
    $select.selectpicker('destroy').empty();
    result.data.forEach(item => {
        const value = item[valueField];
        const text = item[textField];
        $select.append(new Option(text, value));
    });

    $select.selectpicker('refresh');
   
}


async function loadUpdateSelectPickerOptions(apiUrl, selectId, valueField, textField) {
    
    $select.selectpicker('refresh');


}
