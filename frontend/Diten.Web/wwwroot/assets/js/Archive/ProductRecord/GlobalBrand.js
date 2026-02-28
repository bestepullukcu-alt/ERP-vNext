'use strict';
document.addEventListener('DOMContentLoaded', function () {
    // DataTable'ı başlatan fonksiyonu çağır
    initGlobalBrandDataTable();

    // Event listener'ları bağla
    bindDeleteRecordEvent();
    bindModalEvents();

    // Export butonları stillerini düzenle
    styleExportButtons();
    modifyDataTableLayout();
    initializeFormValidation();
    initEditGlobalBrand();
    initializeUpdateFormValidation();
    initActiveDisableGlobalBrand();
    const companiesUrl = `${window.ApiBaseUrl}/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId`;


    fetchCompanies(companiesUrl, "add-Company", null, 1);
});

function initGlobalBrandDataTable() {
    const dt_globalBrand_table = document.querySelector('.datatables-globalBrands');

    if (dt_globalBrand_table) {
        const dt_globalBrand = new DataTable(dt_globalBrand_table, {
            ajax: {
                url: `${window.ApiBaseUrl}/services/PvTenant/TenantBrand/GetBrandsByTenantId`,
                method: 'GET',
                dataSrc: 'data'
            },
            columns: [
                { data: 'name' },
                { data: 'abbrevation' },
                { data: 'companyName' },
                { data: 'isActiveStr' },
                { data: null }
            ],
            columnDefs: [
                {
                    targets: 3, // isActiveStr sütunu
                    width: '120px',
                    render: function (data) {
                        return data === 'Yes'
                            ? `<span class="badge bg-label-success text-capitalized">${data}</span>`
                            : `<span class="badge bg-label-danger text-capitalized">${data}</span>`;
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    width: '130px',
                    searchable: false,
                    orderable: false,
                    render: (data, type, full) => {
                        return `
                            <div class="d-flex align-items-center">
                                <a href="javascript:;" class="btn btn-icon activate-disabled-record" data-id="${full.id}" data-status="${full.isActiveStr}">
                                    <i class="icon-base bx bx-power-off icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon edit-record" data-id="${full.id}" data-name="${full.name}" data-abbrevation="${full.abbrevation}" data-companyid="${full.companyId}">
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
            pageLength: 100,
            layout: {
                topStart: {
                    rowClass: 'row mx-3 my-0 justify-content-between',
                    features: [
                        {
                            pageLength: {
                                menu: [10, 25, 50, 100],
                                value:100,
                                text: '_MENU_'
                            }
                        }
                    ]
                },
                topEnd: {
                    features: [
                        {
                            search: {
                                placeholder: 'Search Brand',
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
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">Add New Brand</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#offcanvasAddGlobalBrand'
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
                searchPlaceholder: 'Search Brand',
                paginate: {
                    next: '<i class="icon-base bx bx-chevron-right icon-18px"></i>',
                    previous: '<i class="icon-base bx bx-chevron-left icon-18px"></i>'
                }
            },
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: function (row) {
                            const data = row.data();
                            return 'Details of ' + data['full_name'];
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
            const response = await fetch(`${window.ApiBaseUrl}/services/PvTenant/TenantBrand/DeleteTenantBrand`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    id: recordIdToDelete, modifiedBy: userName
                })
            });

            const result = await response.json();

            if (result.isSuccessful && result.data === true) {
                const table = $('.datatables-globalBrands').DataTable();
                table.ajax.reload();

                // Modalı kapat
                bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
            } else {
                alert("Silme işlemi başarısız oldu.");
                console.warn(result.errors); // Hata detayları varsa konsola yaz
            }
        } catch (error) {
            console.error(error);
            alert("Bir hata oluştu.");
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

function styleExportButtons() {
    // To remove default btn-secondary in export buttons
    $('.dt-buttons > .btn-group > button').removeClass('btn-secondary');
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
    const addNewGlobalBrandForm = document.getElementById('addNewGlobalBrandForm');

    if (!addNewGlobalBrandForm) return;

    const fv = FormValidation.formValidation(addNewGlobalBrandForm, {
        fields: {
            globalBrandname: {
                validators: {
                    notEmpty: {
                        message: 'Please enter brand name '
                    }
                }
            },
            globalBrandAbbrevation: {
                validators: {
                    notEmpty: {
                        message: 'Please enter abbrevation'
                    }
                }
            },
             addCompany: {
                validators: {
                    notEmpty: {
                        message: 'Please select company'
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

        const brandName = document.getElementById('add-globalbrand-name').value;
        const brandAbb = document.getElementById('add-globalbrand-abbrevation').value;
        const company = document.getElementById('add-Company').value;
        const userName = window.getUserName();
        const formData = {
            name: brandName,
            abbrevation: brandAbb,
            tenantId: null,
            createdBy: userName,
            companyId: company
        };

        fetch(`${window.ApiBaseUrl}/services/PvTenant/TenantBrand/CreateTenantBrand`, {
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

                const table = $('.datatables-globalBrands').DataTable();
                table.ajax.reload();
            })
            .catch(error => {
                console.error(error);
                alert('Kayıt sırasında bir hata oluştu.');
            });
    });
}

function initEditGlobalBrand() {
    $(document).on('click', '.edit-record', function () {
        const id = $(this).data('id');
        const name = $(this).data('name');
        const abbrevation = $(this).data('abbrevation');
        const companyid = $(this).data('companyid');

        $('#update-globalbrand-name').val(name);
        $('#update-globalbrand-abbrevation').val(abbrevation);
        $('#updateGlobalBrandForm').attr('data-brand-id', id);
        const url = `${window.ApiBaseUrl}/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId`;

        fetchCompanies(url, "update-Company", companyid, 1);
        const offcanvasEl = document.getElementById('updateGlobalBrand');
        const bsOffcanvas = new bootstrap.Offcanvas(offcanvasEl);
        bsOffcanvas.show();
    });
}

function initializeUpdateFormValidation() {
    const updateForm = document.getElementById('updateGlobalBrandForm');

    if (!updateForm) return;

    const fv = FormValidation.formValidation(updateForm, {
        fields: {
            updateGlobalBrandName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter brand name'
                    }
                }
            },
            updateGlobalBrandAbbrevation: {
                validators: {
                    notEmpty: {
                        message: 'Please enter abbrevation'
                    }
                }
            }
        },
        updateCompany: {
            validators: {
                notEmpty: {
                    message: 'Please select company'
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
        const form = document.getElementById('updateGlobalBrandForm');
        const brandId = $('#updateGlobalBrandForm').attr('data-brand-id');
        const userName = window.getUserName();
        const company = document.getElementById('update-Company').value;

        const updatedData = {
            id: brandId,
            name: document.getElementById('update-globalbrand-name').value,
            tenantId:"",
            abbrevation: document.getElementById('update-globalbrand-abbrevation').value,
            modifiedBy: userName,
            companyId: company

        };
        fetch(`${window.ApiBaseUrl}/services/PvTenant/TenantBrand/UpdateTenantBrand`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(updatedData)
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                if (data.isSuccessful) {
                    const table = $('.datatables-globalBrands').DataTable();
                    table.ajax.reload();

                    const offcanvasEl = document.getElementById('updateGlobalBrand');
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

function initActiveDisableGlobalBrand() {
    $(document).on('click', '.activate-disabled-record', function () {

        const brandId = $(this).data('id');
        const status = $(this).data('status');
        const userName = window.getUserName();

        const statusData = {
            id: brandId,
            isActive: status === 'Yes' ? false : true,            
            modifiedBy: userName
        };
        fetch(`${window.ApiBaseUrl}/services/PvTenant/TenantBrand/activate-disable`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(statusData)
        })
            .then(response => response.json())
            .then(data => {
                if (data.isSuccessful) {
                    const table = $('.datatables-globalBrands').DataTable();
                    table.ajax.reload();                  
                    showToast('The record has been changed status successfully.', "success");
                } else {
                    const errorMessage = data.errors?.join('<br>') || 'An error occurred during the update.';
                    showToast(errorMessage, "error");
                }
            })
            .catch(error => {
                console.error(error);

                showToast('An error occurred while connecting to the server.', "error");
            });



    });
}

//isTenantStatus : 1 ise sadece tenant olan company için filtre yapılır 0 ise tüm companyler gelmeli
async function fetchCompanies(apiUrl, selectElementId, selectedCompanyId, isTenantStatus) {
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
        if (isTenantStatus==1) {
            companyList = companyList.filter(company => company.isTenant === true);
        }

        // Seçilecek ID belirleniyor
        let autoSelectId = null;
        if (selectedCompanyId != null && selectedCompanyId !== "") {
            autoSelectId = selectedCompanyId;
        } else if (companyList.length === 1 && (selectedCompanyId == null || selectedCompanyId == "")) {
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
