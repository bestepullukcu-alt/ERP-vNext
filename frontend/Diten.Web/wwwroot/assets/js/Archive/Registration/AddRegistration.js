'use strict';
const port2 = protocol === 'https:' ? '5055' : '5050';

document.addEventListener('DOMContentLoaded', function () {
    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;


    const brandUrl = `${window.ApiBaseUrl}/services/PvTenant/TenantBrand/GetBrandsByTenantId`;
    fetchBrand(brandUrl, "ddl-global-brand");

    const countryUrl = `${window.ApiBaseUrl}/services/PvTenant/Tenant/GetCountriesByTenantId`;
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
            fetchGlobalSkus(`${window.ApiBaseUrl}/services/PvOrganization/GlobalSku/GetGlobalSkusByBrandId`, selectedBrandId, 'ddl-global-sku');

            fetchMaHolders(`${window.ApiBaseUrl}/services/PvTenant/TenantBrand/GetOrganizationsByBrandId`, selectedBrandId, 'ddl-ma-holder');


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

                fetchAndRenderForms(`${window.ApiBaseUrl}/services/PvOrganization/GlobalSku/GetGlobalSkuById`, selectedSkuId, "dvPackaging");
                fetchAndRenderFormCards(`${window.ApiBaseUrl}/services/PvOrganization/GlobalSku/GetGlobalSkuById`, selectedSkuId, "dvActiveIngredients");



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
        const disableStatus = urlParams.get('disabledStatus') ?? 0;
        const maStatus = urlParams.get('maStatus') ?? 0;

        const globalBrand = document.getElementById('ddl-global-brand').value;
        const globalSku = document.getElementById('ddl-global-sku').value;
        const countryBrandName = document.getElementById('txt-country-brand-name').value;
        const maHolder = document.getElementById('ddl-ma-holder').value;
        const country = document.getElementById('ddl-country').value;
        const lqppvUser = document.getElementById('ddl-lqppv').value;
        const psmfNumber = document.getElementById('txt-psmf-number').value;
        const atcCode = document.getElementById('txt-atc').value;

        const fltStart = document.querySelector('#dtStartDate')._flatpickr;
        const startDate = fltStart.selectedDates[0];
        let isoDateStart;
        if (startDate) {
            isoDateStart = new Date(startDate.getTime() - startDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }
        const fltEnd = document.querySelector('#dtEndDate')._flatpickr;
        const endDate = fltEnd.selectedDates[0];
        let isoDateEnd;
        if (endDate) {
            isoDateEnd = new Date(endDate.getTime() - endDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }

        const comments = window.commentEditor.root.innerHTML;
        const userName = window.getUserName();
        if (!id) {
            const formData = {
                globalBrandId: globalBrand, // bunu dinamik alacaksan değiştir
                globalSkuId: globalSku, // bunu da dinamik al
                countryId: country,
                countryBrandName: countryBrandName,
                organizationId: maHolder, // örnek
                startDate: isoDateStart,
                endDate: isoDateEnd,
                localQPPVuserId: lqppvUser,
                psmfNumber: psmfNumber,
                atcCode: atcCode,
                comment: comments,
                createdBy: userName, // oturumdan alınıyorsa güncelle
                
            };
            
            fetch(`${window.ApiBaseUrl}/services/PvOrganization/Ma/CreateMa`, {
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

                        window.location.href = '/registration/marketing-authorization';


                        //UploadDocument(data.data);

                    }
                    else {
                        const errorMessage = data.errors?.join('<br>') || 'An error occurred during the update.';
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
                id: id,
                maStatus: maStatus,
                globalBrandId: globalBrand, // bunu dinamik alacaksan değiştir
                globalSkuId: globalSku, // bunu da dinamik al
                countryId: country,
                countryBrandName: countryBrandName,
                organizationId: maHolder, // örnek
                startDate: isoDateStart,
                endDate: isoDateEnd,
                localQPPVuserId: lqppvUser,
                psmfNumber: psmfNumber,
                atcCode: atcCode,
                comment: comments,
                modifiedBy: userName, // oturumdan alınıyorsa güncelle

            };

            console.log(formData);

            fetch(`${window.ApiBaseUrl}/services/PvOrganization/Ma/UpdateMaStatus`, {
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

                        window.location.href = '/registration/marketing-authorization';


                        //UploadDocument(data.data);

                    }
                    else {
                        const errorMessage = data.errors?.join('<br>') || 'An error occurred during the update.';
                        showToast(errorMessage, "error");
                    }



                })
                .catch(error => {
                    console.error(error);
                    showToast('Kayıt sırasında bir hata oluştu.', "error");

                });
        }

    });
}

async function loadMa() {

    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;
    const maStatus = urlParams.get('maStatus') ?? 0;
    const response = await fetch(`${window.ApiBaseUrl}/services/PvOrganization/Ma/GetMarketingAuthorizationById/${id}`); // API adresin 
    const result = await response.json();
    const item = result.data;
   
    $('#ddl-global-brand').val(item.globalBrandId).trigger('change');
    let interval = setInterval(function () {
        if ($('#ddl-global-sku option').length > 1) { // veya belirli option'u kontrol et
            $('#ddl-global-sku').val(item.globalSkuId).trigger('change');
            clearInterval(interval);
        }
    }, 100);
    $('#txt-country-brand-name').val(item.countryBrandName);
    $('#ddl-ma-holder').val(item.organizationId).trigger('change');
    $('#ddl-country').val(item.countryId).trigger('change');
    $('#ddl-lqppv').val(item.localQPPVuserId).trigger('change');
    $('#txt-psmf-number').val(item.psmfNumber);
    $('#txt-atc').val(item.atcCode);
    window.commentEditor.root.innerHTML = item.comment;

    if (item.globalSkuId) {

        fetchAndRenderForms(`${window.ApiBaseUrl}/services/PvOrganization/GlobalSku/GetGlobalSkuById`, item.globalSkuId, "dvPackaging");
        fetchAndRenderFormCards(`${window.ApiBaseUrl}/services/PvOrganization/GlobalSku/GetGlobalSkuById`, item.globalSkuId, "dvActiveIngredients");



    }
    else {
        document.getElementById("dvPackaging").innerHTML = '';
        document.getElementById("dvActiveIngredients").innerHTML = '';
    }




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

    


    //if (maStatus == 2 || maStatus == 3) {
    //    $('#ddl-global-brand').prop('disabled', true);
    //    $('#ddl-global-sku').prop('disabled', true);
    //    $('#txt-country-brand-name').prop('disabled', true);
    //    $('#ddl-ma-holder').prop('disabled', true);
    //    $('#ddl-country').prop('disabled', true);
    //    //dtStartDate._flatpickr.altInput.setAttribute('disabled', true);
    //    //dtEndDate._flatpickr.altInput.setAttribute('disabled', true);
    //    $('#ddl-lqppv').prop('disabled', true);
    //    $('#txt-psmf-number').prop('disabled', true);
    //    $('#txt-atc').prop('disabled', true);
    //    window.commentEditor.enable(false);
    //    document.querySelector('.ql-toolbar').classList.add('ql-disabled');
    //}




    if (disableStatus == 1) {

        $('#submitButton').prop('disabled', true);
        $('#ddl-global-brand').prop('disabled', true);
        $('#ddl-global-sku').prop('disabled', true);
        $('#txt-country-brand-name').prop('disabled', true);
        $('#ddl-ma-holder').prop('disabled', true);
        $('#ddl-country').prop('disabled', true);
        dtStartDate._flatpickr.altInput.setAttribute('disabled', true);
        dtEndDate._flatpickr.altInput.setAttribute('disabled', true);
        $('#ddl-lqppv').prop('disabled', true);
        $('#txt-psmf-number').prop('disabled', true);
        $('#txt-atc').prop('disabled', true);
        window.commentEditor.enable(false);
        
        document.querySelector('.ql-toolbar').classList.add('ql-disabled');
    }


}

