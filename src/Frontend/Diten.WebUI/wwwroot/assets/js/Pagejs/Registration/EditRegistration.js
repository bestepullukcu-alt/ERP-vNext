'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const port2 = protocol === 'https:' ? '5055' : '5050';
const port3 = protocol === 'https:' ? '5060' : '5053';
document.addEventListener('DOMContentLoaded', function () {
    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;

    const brandUrl = `${protocol}//${domain}:${port}/services/PvTenant/TenantBrand/GetBrandsByTenantId`;
    fetchBrand(brandUrl, "ddl-global-brand");

    const countryUrl = `${protocol}//${domain}:${port}/services/PvTenant/Tenant/GetCountriesByTenantId`;
    fetchCountries(countryUrl, "ddl-country");

    const userUrl = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByCompanyId`;
    fetchLqppvUsers(userUrl, ['ddl-lqppv']);

    initializeFormValidation();



    if (id) {

        loadMa();
    }
    
});


$(document).ready(function () {
    $('#ddl-global-brand').on('change', function () {
        const selectedBrandId = $(this).val();
        const selectedText = $(this).find('option:selected').text();
        console.log("Seçilen brand ID:", selectedBrandId);

        if (selectedBrandId) {
            fetchGlobalSkus(`${protocol}//${domain}:${port}/services/PvOrganization/GlobalSku/GetGlobalSkusByBrandId`, selectedBrandId, 'ddl-global-sku');

            fetchMaHolders(`${protocol}//${domain}:${port}/services/PvTenant/TenantBrand/GetOrganizationsByBrandId`, selectedBrandId, 'ddl-ma-holder');


        } else {
            const skuSelect = $('#ddl-global-sku');
            skuSelect.empty().trigger('change');

            const maSelect = $('#ddl-ma-holder');
            maSelect.empty().trigger('change');
        }

        $('#txt-country-brand-name').val(selectedText);



    });

    $('#ddl-global-sku').on('change', function () {
        const selectedSkuId = $(this).val();
        console.log("Seçilen sku ID:", selectedSkuId);
        const urlParams = new URLSearchParams(window.location.search);
        const id = urlParams.get('id');

        if (!id) {
            if (selectedSkuId) {

                fetchAndRenderForms(`${protocol}//${domain}:${port}/services/PvOrganization/GlobalSku/GetGlobalSkuById`, selectedSkuId, "dvPackaging");
                fetchAndRenderFormCards(`${protocol}//${domain}:${port}/services/PvOrganization/GlobalSku/GetGlobalSkuById`, selectedSkuId, "dvActiveIngredients");



            }
            else {
                document.getElementById("dvPackaging").innerHTML = '';
                document.getElementById("dvActiveIngredients").innerHTML = '';
            }
        }
        else {
            document.getElementById("dvPackaging").innerHTML = '';
            document.getElementById("dvActiveIngredients").innerHTML = '';
        }


        



    });


});


async function fetchLqppvUsers(apiUrl, selectElementIds) {
    try {
        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const userName = window.getUserName();
        // Tüm select elementlerini işle
        selectElementIds.forEach(selectElementId => {
            const selectElement = document.getElementById(selectElementId);

            if (!selectElement) {
                console.warn(`Element with ID ${selectElementId} not found.`);
                return;
            }

            // Select2 varsa temizle
            if ($(selectElement).hasClass("select2")) {
                $(selectElement).empty().trigger('change');
            } else {
                selectElement.innerHTML = '';
            }

            // İlk boş option
            const defaultOption = new Option("Select a user", "", false, false);
            selectElement.appendChild(defaultOption);

            let selectedValue = "";
            // Seçenekleri ekle
            data.data.forEach(user => {
                const value = user.id ?? user.fullName;
                const option = new Option(user.fullName, value, false, false);
                selectElement.appendChild(option);

                if (user.fullName === userName) {
                    selectedValue = value;
                }


            });

            // Eğer sadece 1 item varsa, otomatik seç
            if (data.data.length === 1) {
                const singleItem = data.data[0];
                selectedValue = singleItem.id ?? singleItem.fullName;
            }

            if (selectedValue) {
                $(selectElement).val(selectedValue);
            }

            // Select2 varsa trigger
            if ($(selectElement).hasClass("select2")) {
                $(selectElement).trigger('change');
            }
        });

    } catch (error) {
        console.error("Kullanıcılar alınırken hata oluştu:", error);
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

        // İlk boş option
        const defaultOption = new Option("Select a global brand", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(brand => {
            const value = brand.id ?? brand.name;
            const option = new Option(brand.name, value, false, false);
            selectElement.appendChild(option);
        });

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Brandler alınırken hata oluştu:", error);
    }
}

async function fetchGlobalSkus(apiUrl, brandId, selectElementId) {
    try {
        const response = await fetch(`${apiUrl}/${encodeURIComponent(brandId)}`);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const selectElement = document.getElementById(selectElementId);

        // Select2 varsa temizle
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).empty().trigger('change');
        } else {
            selectElement.innerHTML = '';
        }

        // Boş option
        const defaultOption = new Option("Select a SKU", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(sku => {
            const option = new Option(sku.name, sku.id, false, false);
            selectElement.appendChild(option);
        });

        if (data.data.length === 1) {
            const onlySku = data.data[0];
            selectElement.value = onlySku.id;

            if ($(selectElement).hasClass("select2")) {
                $(selectElement).val(onlySku.id).trigger('change');
            }
        }

        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Global SKU'lar alınırken hata oluştu:", error);
    }
}

async function fetchMaHolders(apiUrl, brandId, selectElementId) {
    try {
        const response = await fetch(`${apiUrl}/${encodeURIComponent(brandId)}`);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const selectElement = document.getElementById(selectElementId);

        // Select2 varsa temizle
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).empty().trigger('change');
        } else {
            selectElement.innerHTML = '';
        }

        // Boş option
        const defaultOption = new Option("Select a organization", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(organization => {
            const option = new Option(organization.organizationName, organization.organizationId, false, false);
            selectElement.appendChild(option);
        });

        if (data.data.length === 1) {
            const onlyOrganization = data.data[0];
            selectElement.value = onlyOrganization.organizationId;

            if ($(selectElement).hasClass("select2")) {
                $(selectElement).val(onlyOrganization.organizationId).trigger('change');
            }
        }

        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Ma Holder'lar alınırken hata oluştu:", error);
    }
}

async function fetchAndRenderForms(apiUrl, brandId, containerId) {
    try {
        const response = await fetch(`${apiUrl}/${encodeURIComponent(brandId)}`);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        console.log("API response:", data);
        const container = document.getElementById(containerId);
        if (!container) {
            console.error("Container bulunamadı:", containerId);
            return;
        }

        // Temizle
        container.innerHTML = '';

        // Eğer data.data boşsa uyarı göster
        if (!data.data) {
            container.innerHTML = '<p>Data not found.</p>';
            return;
        }
        const product = data.data;
        const forms = Array.isArray(product.forms) ? product.forms : [];

        if (forms.length === 0) {
            container.innerHTML = '<p>No forms found.</p>';
            return;
        }

        // Tablo başlığı
        const table = document.createElement('table');
        table.className = 'table border-top';

        table.innerHTML = `
            <thead>
                <tr>
                    <th>TYPE</th>
                    <th>Packaging Details</th>
                    <th>Volume Quantity</th>
                </tr>
            </thead>
        `;

        const tbody = document.createElement('tbody');

        // data.data içindeki tüm ürünlerin forms listesini döngüyle ekle
        forms.forEach(form => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>${form.formTypeName || ''}</td>
                <td>${form.formName || ''}</td>
                <td>${form.dosage || ''}</td>
            `;
            tbody.appendChild(tr);
        });

        table.appendChild(tbody);
        container.appendChild(table);

    } catch (error) {
        console.error("Forms verisi alınırken hata oluştu:", error);
    }
}

async function fetchAndRenderFormCards(apiUrl, brandId, containerId) {
    try {
        const response = await fetch(`${apiUrl}/${encodeURIComponent(brandId)}`);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const container = document.getElementById(containerId);
        if (!container) {
            console.error("Container bulunamadı:", containerId);
            return;
        }

        container.innerHTML = '';

        if (!data.data) {
            container.innerHTML = '<p>Data not found.</p>';
            return;
        }

        const product = data.data;
        const forms = Array.isArray(product.forms) ? product.forms : [];

        if (forms.length === 0) {
            container.innerHTML = '<p>No forms found.</p>';
            return;
        }

        forms.forEach(form => {
            // Card div
            const cardDiv = document.createElement('div');
            cardDiv.className = 'card mb-6';

            // Card header
            const cardHeader = document.createElement('div');
            cardHeader.className = 'card-header';
            cardHeader.innerHTML = `<h5 class="card-title m-0">${form.formName || 'Unnamed Form'}</h5> <small class="text-muted">All active ingredients (API) and excipients found in the licensed formulation.</small>`;

            // Card body
            const cardBody = document.createElement('div');
            cardBody.className = 'card-body p-3';

            // Table oluştur
            const table = document.createElement('table');
            table.className = 'table border-top';

            table.innerHTML = `
                <thead>
                    <tr>
                        <th>Active Ingredient</th>
                        <th>Type</th>
                        <th>Dosage / Volume</th>
                        
                    </tr>
                </thead>
            `;

            const tbody = document.createElement('tbody');

            const ingredients = Array.isArray(form.activeIngredients) ? form.activeIngredients : [];

            ingredients.forEach(ingredient => {
                const tr = document.createElement('tr');
                tr.innerHTML = `
                    <td>${ingredient.activeIngredientName || ''}</td>
                    <td>${ingredient.activeIngredientTypeName || ''}</td>
                    <td>${ingredient.amount || ''} ${ingredient.uniteName || ''}</td>
                `;
                tbody.appendChild(tr);
            });

            table.appendChild(tbody);
            cardBody.appendChild(table);

            cardDiv.appendChild(cardHeader);
            cardDiv.appendChild(cardBody);

            container.appendChild(cardDiv);
        });

    } catch (error) {
        console.error("Forms verisi alınırken hata oluştu:", error);
    }
}

function initializeFormValidation() {
    const maForm = document.getElementById('addMAForm');

    if (!maForm) return;

    const fv = FormValidation.formValidation(maForm, {
        fields: {
            GlobalBrand: {
                validators: {
                    notEmpty: {
                        message: 'Please select a global brand'
                    }
                }
            },
            GlobalSku: {
                validators: {
                    notEmpty: {
                        message: 'Please select a global sku'
                    }
                }
            },
            MaHolder: {
                validators: {
                    notEmpty: {
                        message: 'Please select a marketing authorization holder'
                    }
                }
            },
            Country: {
                validators: {
                    notEmpty: {
                        message: 'Please select a country'
                    }
                }
            },
            dateStart: {
                validators: {
                    notEmpty: {
                        message: 'Please input start date'
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

        const urlParams = new URLSearchParams(window.location.search);
        const id = urlParams.get('id');
        const maStatus = urlParams.get('maStatus') ?? 0;

        const maNumber = document.getElementById('txt-ma-number').value;
        const fltMa = document.querySelector('#dtMaDate')._flatpickr;
        const maDate = fltMa.selectedDates[0];
        let isoDateMa;
        if (maDate) {
            isoDateMa = new Date(maDate.getTime() - maDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }

        const fltEnd = document.querySelector('#dtEndDate')._flatpickr;
        const dtEndDate = fltEnd.selectedDates[0];
        let isoDateEnd;
        if (dtEndDate) {
            isoDateEnd = new Date(dtEndDate.getTime() - dtEndDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }

        const fltStart = document.querySelector('#dtStartDate')._flatpickr;
        const dtStartDate = fltStart.selectedDates[0];
        let isoDateStart;
        if (dtStartDate) {
            isoDateStart = new Date(dtStartDate.getTime() - dtStartDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }

        const userName = window.getUserName();
        const file = document.getElementById('formFile').files[0];

        const Mayear = maDate.getFullYear();
        const Mamonth = String(maDate.getMonth() + 1).padStart(2, '0'); // Ay 0 tabanlıdır, +1 yap
        const Maday = String(maDate.getDate()).padStart(2, '0');

        const dateOnlyMa = `${Mayear}-${Mamonth}-${Maday}`;


        const formData = new FormData();
        formData.append('MaId', id);
        formData.append('MaStatus', maStatus);
        formData.append('MaNumber', maNumber);
        if (maStatus==2) {

            formData.append('MaApprovalDate', dateOnlyMa);
        }
        else if (maStatus ==3) {

            formData.append('MaRejectDate', dateOnlyMa);
        }
        if (file) formData.append('File', file);
        formData.append('ModifiedBy', userName);
        if (dtEndDate) {
            const dateOnlyEnd = dtEndDate.toISOString().split('T')[0];
            formData.append('EndDate', dateOnlyEnd);
        }
        if (dtStartDate) {
            const Startyear = dtStartDate.getFullYear();
            const Startmonth = String(dtStartDate.getMonth() + 1).padStart(2, '0'); // Ay 0 tabanlıdır, +1 yap
            const Startday = String(dtStartDate.getDate()).padStart(2, '0');

            const dateOnlyStart = `${Startyear}-${Startmonth}-${Startday}`;

            formData.append('StartDate', dateOnlyStart);
        }




        fetch(`${protocol}//${domain}:${port}/services/PvOrganization/Ma/CompleteMa`, {
            method: 'POST',
            body: formData
        })
            .then(response => response.json())
            .then(result => {

                if (result.data) {
                    window.location.href = '/registration/marketing-authorization';
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


       

    });
}


async function loadMa() {

    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;
    const maStatus = urlParams.get('maStatus') ?? 0;
    const response = await fetch(`${protocol}//${domain}:${port}/services/PvOrganization/Ma/GetMarketingAuthorizationById/${id}`); // API adresin 
    const result = await response.json();
    const item = result.data;
    document.getElementById("downloadSection").style.display = "none";
    document.getElementById("documentName").textContent = "";
    document.getElementById("downloadLink").style.display = "none";
    $('#ddl-global-brand').val(item.globalBrandId).trigger('change');
    let interval = setInterval(function () {
        if ($('#ddl-global-sku option').length > 1) { // veya belirli option'u kontrol et
            $('#ddl-global-sku').val(item.globalSkuId).trigger('change');
            clearInterval(interval);
        }
    }, 100);    $('#txt-country-brand-name').val(item.countryBrandName);
    $('#ddl-ma-holder').val(item.organizationId).trigger('change');
    $('#ddl-country').val(item.countryId).trigger('change');
    $('#ddl-lqppv').val(item.localQPPVuserId).trigger('change');
    $('#txt-psmf-number').val(item.psmfNumber);
    $('#txt-atc').val(item.atcCode);
    window.commentEditor.root.innerHTML = item.comment;

    if (item.globalSkuId) {

        fetchAndRenderForms(`${protocol}//${domain}:${port}/services/PvOrganization/GlobalSku/GetGlobalSkuById`, item.globalSkuId, "dvPackaging");
        fetchAndRenderFormCards(`${protocol}//${domain}:${port}/services/PvOrganization/GlobalSku/GetGlobalSkuById`, item.globalSkuId, "dvActiveIngredients");



    }
    else {
        document.getElementById("dvPackaging").innerHTML = '';
        document.getElementById("dvActiveIngredients").innerHTML = '';
    }



    $('#txt-ma-number').val(item.maNumber);

    const dtStartDate = document.querySelector('#dtStartDate');
    dtStartDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtStartDate._flatpickr.setDate(item.startDate, false);

    const dtEndDate = document.querySelector('#dtEndDate');
    dtEndDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtEndDate._flatpickr.setDate(item.endDate, false);

    const dtMaDate = document.querySelector('#dtMaDate');
    dtMaDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtMaDate._flatpickr.setDate(item.maDate, false);


    if (maStatus == 2 || maStatus==3) {
        $('#ddl-global-brand').prop('disabled', true);
        $('#ddl-global-sku').prop('disabled', true);
        $('#txt-country-brand-name').prop('disabled', true);
        $('#ddl-ma-holder').prop('disabled', true);
        $('#ddl-country').prop('disabled', true);
        //dtStartDate._flatpickr.altInput.setAttribute('disabled', true);
        //dtEndDate._flatpickr.altInput.setAttribute('disabled', true);
        $('#ddl-lqppv').prop('disabled', true);
        $('#txt-psmf-number').prop('disabled', true);
        $('#txt-atc').prop('disabled', true);
        window.commentEditor.enable(false);
        document.querySelector('.ql-toolbar').classList.add('ql-disabled');
    }




    if (disableStatus == 1) {

        $('#submitButton').prop('disabled', true);
        $('#ddl-global-brand').prop('disabled', true);
        $('#ddl-global-sku').prop('disabled', true);
        $('#txt-country-brand-name').prop('disabled', true);
        $('#ddl-ma-holder').prop('disabled', true);
        $('#ddl-country').prop('disabled', true);
        $('#txt-ma-number').prop('disabled', true);
        $('#formFile').prop('disabled', true);
        dtStartDate._flatpickr.altInput.setAttribute('disabled', true);
        dtEndDate._flatpickr.altInput.setAttribute('disabled', true);
        dtMaDate._flatpickr.altInput.setAttribute('disabled', true);
        $('#ddl-lqppv').prop('disabled', true);
        $('#txt-psmf-number').prop('disabled', true);
        $('#txt-atc').prop('disabled', true);
        window.commentEditor.enable(false);
        $('#formFile').hide();
        document.getElementById("downloadSection").style.display = "flex";
        document.getElementById("documentName").textContent = item.document.documentName;
        const fileUrl = `${protocol}//${domain}:${port3}${item.document.filePath}`;
        const fileName = item.document.documentName;

        const downloadLink = document.getElementById("downloadLink");
        downloadLink.href = fileUrl;
        downloadLink.setAttribute("download", fileName);
        document.getElementById("downloadLink").style.display = "inline-block";

        document.querySelector('.ql-toolbar').classList.add('ql-disabled');
    }


}
