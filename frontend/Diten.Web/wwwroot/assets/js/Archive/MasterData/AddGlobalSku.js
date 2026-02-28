'use strict';
const sampleData = [];
const forAddData = [];
let ingredientData = [];
document.addEventListener('DOMContentLoaded', function () {
    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;

    const lang = localStorage.getItem('language') || 'en';
    const brandUrl = `${window.ApiBaseUrl}/services/PvTenant/TenantBrand/GetBrandsByTenantId`;
    const companiesUrl = `${window.ApiBaseUrl}/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId`;


    fetchBrand(brandUrl, "add-ddlGlobalBrand");
    fetchCompanies(companiesUrl, ["add-ddlCompany", "add-ddlProductionSite", "add-ddlPackagingSite", "add-ddlBatchReleaseSite"]);
    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["SearchPackagingForm"] || "Search Packaging Form";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initPackagingFormDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Dil dosyası yüklenemedi:', error);
            initPackagingFormDataTable("Search Packaging Form", data); // fallback
        });

    modifyDataTableLayout();

    const formTypeUrl = `${window.ApiBaseUrl}/services/PvTenant/TenantPharmaceuticalForm/GetPharmaceuticalFormTypes`;

    fetchFormType(formTypeUrl, "ddlFormType");
    fetchFormType(formTypeUrl, "updateDdlFormType");
    initializeFormValidation();
    bindDeleteRecordEvent();
    initEditPackagingForm();
    document.getElementById('submitButton').addEventListener('click', handleSkuFormSubmit);

    if (id) {

        loadGlobalSkuInformation();
       
    } 

    if (disableStatus==1) {
        $('#submitButton').prop('disabled', true);
    }

});

$(document).ready(function () {
    $('#ddlFormType').on('change', function () {
        const selectedBrandId = $(this).val();
        const selectedText = $(this).find('option:selected').text();
        console.log("Seçilen brand ID:", selectedBrandId);

        if (selectedBrandId) {
            fetchFormsByFormTypeId(`${window.ApiBaseUrl}/services/PvTenant/TenantPharmaceuticalForm/GetPharmaceuticalFormByFormTypeId`, 'ddlForm', null, false);

         

        } else {
            const skuSelect = $('#ddlForm');
            skuSelect.empty().trigger('change');

           
        }

        



    });



});



function initPackagingFormDataTable(placeholderText, lanData) {
    const dt_packagingForm_table = document.querySelector('.packagingForm-table');
    if (dt_packagingForm_table) {
        const urlParams = new URLSearchParams(window.location.search);
        const disableStatus = urlParams.get('disabledStatus') ?? 0;
        const dt_packagingForm = new DataTable(dt_packagingForm_table, {
            data: sampleData,
            columns: [
                { data: 'id' },                          // 1. sütun (görünür)
                //{ data: 'formTypeId' },                // 3. sütun (görünür)
                { data: 'formTypeName' },                // 3. sütun (görünür)
                //{ data: 'formId' },                // 3. sütun (görünür)
                { data: 'formName' },                    // 5. sütun (görünür)
                { data: 'dosage' },  
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
                    targets: 1, // isActiveStr sütunu
                    render: function (data, type, full, meta) {

                        return `<span class="badge bg-label-success text-capitalized">${data}</span>`;
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    searchable: false,
                    orderable: false,
                    render: (data, type, full) => {


                        const disabledClass = disableStatus == 1 ? 'disabled opacity-50' : '';

                        return `
                            <div class="d-flex align-items-center">
                                <a href="javascript:;" class="btn btn-icon ${disabledClass} edit-record" data-id="${full.id}" data-formtypeid="${full.formTypeId}" data-formid="${full.formId}" data-dosage="${full.dosage}">
                                    <i class="icon-base bx bx-edit-alt icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon ${disabledClass} delete-record " data-id="${full.id}">
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
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block" data-i18n="Add">Add</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#AddPackagingForm'
                                    },
                                    enabled: disableStatus != 1
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
                            return 'Details of ' + data['formName'];
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


async function fetchFormType(apiUrl, selectElementId) {
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
        const onlyOneItem = data.data.length === 1;

        // İlk boş option
        const defaultOption = new Option("Select a form type", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(formType => {
            const value = formType.id ?? formType.name;
            const isSelected = onlyOneItem; // sadece 1 item varsa seçili olsun
            const option = new Option(formType.name, value, isSelected, isSelected);
            selectElement.appendChild(option);
        });

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }
    } catch (error) {
        console.error("An error occurred while fetching form types:", error);
    }
}

async function fetchFormsByFormTypeId(apiUrl, selectElementId, formId,isUpdateStatus) {
    const formTypeId = isUpdateStatus ? document.getElementById('updateDdlFormType').value :  document.getElementById('ddlFormType').value;

    const selectElement = document.getElementById(selectElementId);

    if (!formTypeId) {
        selectElement.disabled = true;
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).val(null).trigger('change');
        } else {
            selectElement.innerHTML = '';
        }
        return;
    }

    try {
        const response = await fetch(`${apiUrl}/${formTypeId}`);
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

        const data = await response.json();

        // Temizle
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).empty().trigger('change');
        } else {
            selectElement.innerHTML = '';
        }
        const onlyOneItem = data.data.length === 1;
        const defaultOption = new Option("Select a form type", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(form => {

            const value = form.id ?? form.name;
            const isSelected = onlyOneItem; // sadece 1 item varsa seçili olsun
            const option = new Option(form.name, value, isSelected, isSelected);
            selectElement.appendChild(option);
        });




        selectElement.disabled = false;

        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

        if (formId) {
            // Form seçenekleri yüklendikten sonra formId'yi ayarla
            $(selectElement).val(formId).trigger('change');  // `val()` ve `trigger('change')` kullanarak form seçimini güncelle
        }

    } catch (error) {
        console.error("Formlar alınırken hata oluştu:", error);
    }
}

function initializeFormValidation() {
    const addPackagingForm = document.getElementById('addNewPackagingForm');
    console.log(addPackagingForm);
    if (!addPackagingForm) return;

    const fv = FormValidation.formValidation(addPackagingForm, {
        fields: {
            formType: {
                validators: {
                    notEmpty: {
                        message: 'Please select a form type'
                    }
                }
            },
            form: {
                validators: {
                    notEmpty: {
                        message: 'Please select a form'
                    }
                }
            },
            txtDosage: {
                validators: {
                    notEmpty: {
                        message: 'Please enter a dosage'
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

        const table = $('.packagingForm-table').DataTable();
        // DataTable'dan ilgili satırı sil
        if (table) {
            // `recordIdToDelete` ile ilgili satırı bul ve sil
            const row = table.row(rowToDelete);
            row.remove();
            table.draw(); // DataTable'ı yeniden çiz
        }
        // forAddData dizisinden ilgili kaydı sil
        console.log(forAddData);
        const indexToDelete = forAddData.findIndex(item => item.id == Number(recordIdToDelete));
        const itemToDelete = forAddData.find(item => item.id == Number(recordIdToDelete));
        
        if (indexToDelete !== -1) {
            // Kaydı sil
            forAddData.splice(indexToDelete, 1);
            if (itemToDelete) {
                const formIdToDelete = itemToDelete.formId;
                ingredientData = ingredientData.filter(ingredient => ingredient.formId !== formIdToDelete);
            }
        }
        
        // Modalı kapat
        bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
    });
}

function handleFormSubmit(fv) {
    fv.on('core.form.valid', function () {
        const formTypeSelect = document.getElementById('ddlFormType');
        const formSelect = document.getElementById('ddlForm');
        const dosage = document.getElementById('txtDosage').value;
        const formType = document.getElementById('ddlFormType').value;
        const form = document.getElementById('ddlForm').value;
        const formTypeText = formTypeSelect.options[formTypeSelect.selectedIndex].text;
        const formText = formSelect.options[formSelect.selectedIndex].text;




        // Aynı kayıt daha önce eklenmiş mi kontrolü
        const exists = sampleData.some(item =>
            item.formTypeName === formTypeText && item.formName === formText && item.dosage === dosage
        );



        if (!exists) {
            const id = forAddData.length + 1;
            const newItem = {
                id: id,
                formTypeId: formType,
                formTypeName: `<span class="badge bg-label-success text-capitalized">${formTypeText}</span>` ,
                formId: form,
                formName: formText,
                dosage: dosage,
                extra: null
            };

            sampleData.push(newItem);
            forAddData.push(newItem);

            const table = $('.packagingForm-table').DataTable();
            table.rows.add([newItem]);
            table.draw();
        }
        else {
            showToast(`A record with the form "${formText}" has already been added.`, "error");
        }
        fv.resetForm(true);
        $('#ddlFormType').val(null).trigger('change');
        $('#ddlForm').val(null).trigger('change');
        sampleData.length = 0;

    });
}
function initEditPackagingForm() {
    $(document).on('click', '.edit-record', async function () {
        const id = $(this).data('id');
        const formTypeId = $(this).data('formtypeid');
        const formId = $(this).data('formid');
        //const country = $(this).data('country');
        const dosage = $(this).data('dosage');


        $('#updateDdlFormType').val(formTypeId).trigger('change');
        const formTypeUrl = `${window.ApiBaseUrl}/services/PvTenant/TenantPharmaceuticalForm/GetPharmaceuticalFormByFormTypeId`;

        fetchFormsByFormTypeId(formTypeUrl, 'updateDdlForm', formId,true);
        //$('#update-company-country').val(country).trigger('change');
        $('#updateTxtDosage').val(dosage);
        $('#updatePackagingForm').attr('data-id', id);
        const offcanvasEl = document.getElementById('UpdatePackagingForm');
        const bsOffcanvas = new bootstrap.Offcanvas(offcanvasEl);
        bsOffcanvas.show();
    });
}

$('#nextBtn').click(function () {
    $('#cardsContainer').empty();

    forAddData.forEach(function (item) {
        const tableId = `ingredientTable_${item.formId}`;
        const cardHtml = `
        <div class="card mb-4" data-form-id="${item.formId}">
        <div class="card-header border-bottom">
        <h5 class="card-title mb-0">${item.formName}</h5>
        </div>
        <div class="card-datatable table-responsive">
         <table id="${tableId}" class="activeIngredient-table_${item.formId} table border-top">
         <thead>
          <tr>
          <th></th>
          <th>Active Ingredient</th>
          <th>Type</th>
          <th>Dosage</th>
          <th>Unite</th>
          <th class="cell-fit" data-i18n="action">Action</th>
          </tr>
          </thead>
          </table>
        </div>
     </div>
    `;
        $('#cardsContainer').append(cardHtml);
        // Her bir table için DataTable başlat
        initDynamicIngredientTable(tableId);
    });

    ingredientData.forEach(function (ingredient) {
        const id = ingredientData.length + 1;
        const newItem = {
            id: id,
            activeIngredientId: ingredient.activeIngredientId,
            activeIngredientName: ingredient.activeIngredientName,
            activeIngredientTypeId: ingredient.activeIngredientTypeId,
            activeIngredientTypeName: ingredient.activeIngredientTypeId == 1 ? `<span class="badge bg-label-primary text-capitalized">${ingredient.activeIngredientTypeName}</span>` : `<span class="badge bg-label-secondary text-capitalized">${ingredient.activeIngredientTypeName}</span>`,
            amount: ingredient.amount,
            formId: ingredient.formId,
            uniteName: ingredient.uniteName,
            uniteId: ingredient.uniteId,
            extra: null
        };
        const tableId = `ingredientTable_${ingredient.formId}`;
        const table = $(`#${tableId}`).DataTable();
        table.rows.add([newItem]);
        table.draw();
    });





});

function initDynamicIngredientTable(tableId) {
    const urlParams = new URLSearchParams(window.location.search);
    const disableStatus = urlParams.get('disabledStatus') ?? 0;
    $(`#${tableId}`).DataTable({
        data: [],
        columns: [
            { data: 'id' },                                                     
            { data: 'activeIngredientName' }, 
            { data: 'activeIngredientTypeName', render: data => data },                
            { data: 'amount' },
            { data: 'uniteName' },
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
                    const disabledClass = disableStatus == 1 ? 'disabled opacity-50' : '';
                    return `
                            <div class="d-flex align-items-center">
                                <a href="javascript:;" class="btn btn-icon edit-record ${disabledClass}" data-id="${full.id}" data-ingredientid="${full.activeIngredientId}" data-ingredienttypeid="${full.activeIngredientTypeId}" data-dosage="${full.amount}" data-uniteid="${full.uniteId}">
                                    <i class="icon-base bx bx-edit-alt icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon ${disabledClass} delete-ingredient-record" data-id="${full.id}">
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
                            placeholder: 'Search Active Ingredient',
                            text: '_INPUT_'
                        }
                    },
                    {
                        buttons: [
                            {
                                text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block" data-i18n="Add">Add API / Excipient</span>',
                                className: 'add-new btn btn-primary',
                                attr: {
                                    'data-bs-toggle': 'offcanvas',
                                    'data-bs-target': '#AddIngredientForm'
                                },
                                action: function (e, dt, node, config) {
                                    // En yakın card'ı bul
                                    const cardElement = $(node).closest('.card')[0];
                                    if (!cardElement) return;

                                    // Form name'i al
                                    const formId = cardElement.getAttribute('data-form-id');
                                    // Gerekirse bir hidden input'a da koyabilirsin
                                    document.querySelector('#AddIngredientForm input[name="activeIngredientFormId"]').value = formId;

                                    const ingredientUrl = `${window.ApiBaseUrl}/services/PvTenant/TenantActiveIngredient/GetActiveIngredientsByTenantId`;
                                    const formTypeUrl = `${window.ApiBaseUrl}/services/PvTenant/TenantPharmaceuticalForm/GetPharmaceuticalFormByFormTypeId/3`;



                                    fetchFormType(ingredientUrl, "ddlIngredient");
                                    fetchFormType(formTypeUrl, "ddlUnite");
                                    initializeIngredientFormValidation();
                                    bindDeleteIngredientRecordEvent();

                                },
                                enabled: disableStatus != 1
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
                        return 'Details of ' + data['ingredientName'];
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
    modifyDataTableLayout();
}

function initializeIngredientFormValidation() {
    const addIngredientForm = document.getElementById('addNewIngredientForm');
    if (!addIngredientForm) return;

    const fv = FormValidation.formValidation(addIngredientForm, {
        fields: {
            ingredientType: {
                validators: {
                    notEmpty: {
                        message: 'Please select a type'
                    }
                }
            },
            ingredient: {
                validators: {
                    notEmpty: {
                        message: 'Please select a ingredient'
                    }
                }
            },
            unite: {
                validators: {
                    notEmpty: {
                        message: 'Please select a unite'
                    }
                }
            },
            ingredientDosage: {
                validators: {
                    notEmpty: {
                        message: 'Please enter a dosage'
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

    handleIngredientFormSubmit(fv);
}

function handleIngredientFormSubmit(fv) {
    fv.on('core.form.valid', function () {
        
        const ingredientTypeSelect = document.getElementById('ddlIngredientType');
        const ingredientSelect = document.getElementById('ddlIngredient');
        const uniteSelect = document.getElementById('ddlUnite');
        const dosage = document.getElementById('txtIngredientDosage').value;
        const ingredientType = document.getElementById('ddlIngredientType').value;
        const ingredient = document.getElementById('ddlIngredient').value;
        const ingredientTypeText = ingredientTypeSelect.options[ingredientTypeSelect.selectedIndex].text;
        const ingredientText = ingredientSelect.options[ingredientSelect.selectedIndex].text;
        const uniteText = uniteSelect.options[uniteSelect.selectedIndex].text;
        const ingredientFormId = document.getElementById('txtActiveIngredientFormId').value;
        const unite = document.getElementById('ddlUnite').value;

        // Aynı kayıt daha önce eklenmiş mi kontrolü
        const exists = ingredientData.some(item =>
            item.activeIngredientTypeId == ingredientType && item.activeIngredientId == ingredient && item.amount == dosage && item.formId == ingredientFormId
        );


        if (!exists) {
            const id = ingredientData.length + 1;
            const newItem = {
                id: id,
                activeIngredientId: ingredient,
                activeIngredientName: ingredientText,
                activeIngredientTypeId: ingredientType,
                activeIngredientTypeName: ingredientType == 1 ? `<span class="badge bg-label-primary text-capitalized">${ingredientTypeText}</span>` : `<span class="badge bg-label-secondary text-capitalized">${ingredientTypeText}</span>` ,               
                amount: dosage,
                formId: ingredientFormId,
                uniteName: uniteText,
                uniteId: unite,
                extra: null
            };
           
            ingredientData.push(newItem);
            const tableId = `ingredientTable_${ingredientFormId}`;
            const table = $(`#${tableId}`).DataTable();
            table.rows.add([newItem]);
            table.draw();
        }
        else {
            showToast(`A record with the form "${ingredientTypeText}" has already been added.`, "error");
        }
        fv.resetForm(true);
       

    });
}



async function fetchBrand(apiUrl, selectElementId) {
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
        const onlyOneItem = data.data.length === 1;

        // İlk boş option
        const defaultOption = new Option("Select a brand", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data
            .filter(brand => brand.isActive) // önce sadece aktifleri al
            .sort((a, b) => a.name.localeCompare(b.name)) // name'e göre alfabetik sırala
            .forEach((brand, index, array) => {
                const value = brand.id ?? brand.name;
                const isSelected = array.length === 1; // sadece 1 item varsa seçili olsun
                const option = new Option(brand.name, value, isSelected, isSelected);
                selectElement.appendChild(option);
            });

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }
    } catch (error) {
        console.error("An error occurred while fetching brands:", error);
    }
}

async function fetchCompanies(apiUrl, selectElementIds) {
    try {
        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const companies = data.data
            .sort((a, b) => a.companyName.localeCompare(b.companyName));

        const onlyOneItem = companies.length === 1;

        selectElementIds.forEach(selectElementId => {
            const selectElement = document.getElementById(selectElementId);
            if (!selectElement) return;

            // Select2 varsa temizle
            if ($(selectElement).hasClass("select2")) {
                $(selectElement).empty().trigger('change');
            } else {
                selectElement.innerHTML = '';
            }

            // İlk boş option
            const defaultOption = new Option("Select a brand", "", false, false);
            selectElement.appendChild(defaultOption);

            companies.forEach(company => {
                const value = company.id ?? company.companyName;
                const isSelected = onlyOneItem;
                const option = new Option(company.companyName, value, isSelected, isSelected);
                selectElement.appendChild(option);
            });

            // Select2 varsa change tetikle
            if ($(selectElement).hasClass("select2")) {
                $(selectElement).trigger('change');
            }
        });
    } catch (error) {
        console.error("An error occurred while fetching brands:", error);
    }
}

function initializeSkuFormValidation() {
    const addSkuForm = document.getElementById('createSkuForm');
    if (!addSkuForm) return;

    const fv = FormValidation.formValidation(addSkuForm, {
        fields: {
            globalSkuBrand: {
                validators: {
                    notEmpty: {
                        message: 'Please select a brand'
                    }
                }
            },
            globalSkuCompany: {
                validators: {
                    notEmpty: {
                        message: 'Please select a company'
                    }
                }
            },
            globalSkuName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter a sku name'
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

    handleSkuFormSubmit(fv);
}

function handleSkuFormSubmit() {
    
    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');

        const globalBrand = document.getElementById('add-ddlGlobalBrand').value;
        const globalSku = document.getElementById('globalSkuName').value;
        const companyId = document.getElementById('add-ddlCompany').value;
        const productionSiteId = document.getElementById('add-ddlProductionSite').value;
        const packagingSiteId = document.getElementById('add-ddlPackagingSite').value;
        const batchReleaseSiteId = document.getElementById('add-ddlBatchReleaseSite').value;
        const formsData = [];
        const activeIngredientData = [];
        const userName = window.getUserName();
        forAddData.forEach(function (item) {

            const newItem = {
                formId: item.formId,
                dosage: item.dosage
            };

            formsData.push(newItem);
        });

        ingredientData.forEach(function (item) {

            const newItem = {
                activeIngredientId: item.activeIngredientId,
                activeIngredientTypeId: item.activeIngredientTypeId,
                amount: item.amount,
                uniteId: item.uniteId,
                formId: item.formId
            };

            activeIngredientData.push(newItem);
        });


    if (!id) {
        const formData = {
            globalBrandId: globalBrand,
            name: globalSku,
            companyId: companyId,
            productionSideCompanyId: productionSiteId,
            packagingSiteCompanyId: packagingSiteId,
            batchReleaseSiteCompanyId: batchReleaseSiteId,
            forms: formsData,
            activeIngredients: activeIngredientData,
            createdBy: userName
        };
        fetch(`${window.ApiBaseUrl}/services/PvOrganization/GlobalSku/CreateGlobalSku`, {
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
                    window.location.href = '/master-data/global-sku';

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
    }
    else {
        const formData = {
            id:id,
            globalBrandId: globalBrand,
            name: globalSku,
            companyId: companyId,
            productionSideCompanyId: productionSiteId,
            packagingSiteCompanyId: packagingSiteId,
            batchReleaseSiteCompanyId: batchReleaseSiteId,
            forms: formsData,
            activeIngredients: activeIngredientData,
            modifiedBy: userName
        };
        fetch(`${window.ApiBaseUrl}/services/PvOrganization/GlobalSku/UpdateGlobalSku`, {
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
                    window.location.href = '/master-data/global-sku';

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
    }





       
        


   
}

function bindDeleteIngredientRecordEvent() {
    let recordIdToDelete = null;
    let rowToDelete = null;

    document.addEventListener('click', function (e) {
        if (e.target.closest('.delete-ingredient-record')) {
            const button = e.target.closest('.delete-ingredient-record');
            recordIdToDelete = button.getAttribute('data-id');
            rowToDelete = button.closest('tr');

            const deleteModal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
            deleteModal.show();
        }
    });

    document.getElementById('confirmDeleteBtn').addEventListener('click', async function () {
        if (!recordIdToDelete) return;

        const ingredientFormId = document.getElementById('txtActiveIngredientFormId').value;

        const tableId = `ingredientTable_${ingredientFormId}`;
        const table = $(`#${tableId}`).DataTable();
        // DataTable'dan ilgili satırı sil
        if (table) {
            // `recordIdToDelete` ile ilgili satırı bul ve sil
            const row = table.row(rowToDelete);
            row.remove();
            table.draw(); // DataTable'ı yeniden çiz
        }
        // forAddData dizisinden ilgili kaydı sil
        const indexToDelete = ingredientData.findIndex(item => item.id == Number(recordIdToDelete));
        if (indexToDelete !== -1) {
            // Kaydı sil
            forAddData.splice(indexToDelete, 1);
        }
        // Modalı kapat
        bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
    });
}

async function loadGlobalSkuInformation() {

    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;

    if (disableStatus == 1) {
        $('#add-ddlGlobalBrand').prop('disabled', true);
        $('#globalSkuName').prop('disabled', true);
        $('#add-ddlCompany').prop('disabled', true);
        $('#add-ddlProductionSite').prop('disabled', true);
        $('#add-ddlPackagingSite').prop('disabled', true);
        $('#add-ddlBatchReleaseSite').prop('disabled', true);
    }
    const response = await fetch(`${window.ApiBaseUrl}/services/PvOrganization/GlobalSku/GetGlobalSkuById/${id}`); // API adresin 
    const result = await response.json();
    const item = result.data;

    $('#add-ddlGlobalBrand').val(item.globalBrandId).trigger('change');
    $('#globalSkuName').val(item.name);
    $('#add-ddlCompany').val(item.companyId).trigger('change');
    $('#add-ddlProductionSite').val(item.productionSideCompanyId).trigger('change');
    $('#add-ddlPackagingSite').val(item.packagingSiteCompanyId).trigger('change');
    $('#add-ddlBatchReleaseSite').val(item.batchReleaseSiteCompanyId).trigger('change');





    item.forms.forEach(form => {
        const id = forAddData.length + 1;
        const newItem = {
            id: id,
            formTypeId:form.formTypeId,
            formTypeName: `<span class="badge bg-label-success text-capitalized">${form.formTypeName}</span>`,
            formId: form.formId,
            formName: form.formName,
            dosage: form.dosage,
            extra: null
        };

        sampleData.push(newItem);
        forAddData.push(newItem);

        const table = $('.packagingForm-table').DataTable();
        table.rows.add([newItem]);
        table.draw();

        form.activeIngredients.forEach(ingredient => {
            const id = ingredientData.length + 1;
            const newItem = {
                id: id,
                activeIngredientId: ingredient.activeIngredientId,
                activeIngredientName: ingredient.activeIngredientName,
                activeIngredientTypeId: ingredient.activeIngredientTypeId,
                activeIngredientTypeName: ingredient.activeIngredientTypeId == 1 ? `<span class="badge bg-label-primary text-capitalized">${ingredient.activeIngredientTypeName}</span>` : `<span class="badge bg-label-secondary text-capitalized">${ingredient.activeIngredientTypeName}</span>`,
                amount: ingredient.amount,
                formId: form.formId,
                uniteName: ingredient.uniteName,
                uniteId: ingredient.uniteId,
                extra: null
            };

            ingredientData.push(newItem);
            //const tableId = `ingredientTable_${form.formId}`;
            //const table = $(`#${tableId}`).DataTable();
            //table.rows.add([newItem]);
            //table.draw();

        });




    });


}


