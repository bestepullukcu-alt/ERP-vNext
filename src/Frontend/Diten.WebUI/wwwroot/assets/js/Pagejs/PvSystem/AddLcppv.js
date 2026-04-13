'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const port2 = protocol === 'https:' ? '5060' : '5053';

dayjs.extend(dayjs_plugin_relativeTime);
let uploadedFiles = [];
let documentsTemp = [];
document.addEventListener('DOMContentLoaded', function () {
    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;


    const countryUrl = `${protocol}//${domain}:${port}/services/PvTenant/Tenant/GetCountriesByTenantId`;
    fetchCountries(countryUrl, "ddlCountry");
    const statusUrl = `${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/GetLcppvStatus`;
    fetchDropdowns(statusUrl, "ddlStatus");

    const dropdownUrl = `${protocol}//${domain}:${port}/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId`;
    fetchDropdownlist(dropdownUrl, "ddlCompany");
    initializeFormValidation();

    if (id) {

        loadLcppv();
    }
    else {
        loadQuestionsFromApi('dvReportDetail');
    }



});

function loadQuestionsFromApi(containerId) {

    fetch(`${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/GetQuestions`)
        .then(response => response.json())
        .then(result => {
            if (result?.data) {
                renderQuestions(containerId, result.data);
            } else {
                console.error('Veri yok veya hatalı format:', result);
            }
        })
        .catch(error => console.error('API error:', error));
}
function renderQuestions(containerId, questions) {
    const container = document.getElementById(containerId);
    if (!container) {
        console.error(`Container not found: #${containerId}`);
        return;
    }
    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;
    const isDisabled = disableStatus == 1 ? 'disabled' : '';

    container.innerHTML = ''; // eski içerikleri temizle

    questions.forEach(q => {

        const questionText = id ? q.questionName : q.name;
        const isYesChecked = q.answer == 1;
        const isNoChecked = q.answer == 2;
        const questionId = id ? q.questionId : q.id;
        const questionDiv = document.createElement('div');
        questionDiv.className = 'mb-4';
        
        const questionHtml = `
            <p class="mb-0">${questionText}</p>
            <div class="form-check my-2 ms-2">
                <input class="form-check-input" type="radio" name="${questionId}" id="${questionId}_yes" value="yes" ${isYesChecked ? 'checked' : ''} ${isDisabled} />
                <label class="form-check-label text-heading" for="${questionId}_yes">Yes</label>
            </div>
            <div class="form-check mt-4 ms-2">
                <input class="form-check-input" type="radio" name="${questionId}" id="${questionId}_no" value="no" ${isNoChecked ? 'checked' : ''} ${isDisabled} />
                <label class="form-check-label text-heading" for="${questionId}_no">No</label>
            </div>
        `;

        questionDiv.innerHTML = questionHtml;
        container.appendChild(questionDiv);
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

async function fetchDropdowns(apiUrl, selectElementId) {
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
        const defaultOption = new Option("Select a status", "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(status => {
            const value = status.id ?? status.name;
            const color = status.id == 1 ? 'green' :
                status.id == 2 ? 'red' : 'black';
            const option = new Option(status.name, value, false, false);
            option.style.color = color;
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
        const defaultOption = new Option("Select a company", "", false, false);
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


document.getElementById('uploadIcon').addEventListener('click', function () {
    document.getElementById('fileInput').click(); // ikon tıklanınca input tetiklenir
});

document.getElementById('fileInput').addEventListener('change', function (event) {
    const file = event.target.files[0];
    if (file) {
        uploadedFiles.push(file); // Dosyayı listeye ekle
        displayFile(file);
        // Dosya seçildiğinde burada işlemler yapılabilir
        console.log("Seçilen dosya:", file.name);
        // İstersen hemen form-data ile sunucuya gönderebilirsin
    }
});
function displayFile(file) {
    const uploadItems = document.getElementById('uploadItems');

    const itemDiv = document.createElement('div');
    itemDiv.classList.add('d-flex', 'align-items-center', 'justify-content-between', 'mb-2', 'border', 'rounded', 'p-2');

    const fileName = document.createElement('span');
    fileName.textContent = file.name;
    fileName.classList.add('text-truncate', 'me-2');

    const buttonsDiv = document.createElement('div');
    buttonsDiv.classList.add('d-flex', 'align-items-center');

    const deleteBtn = document.createElement('a');
    deleteBtn.href = 'javascript:;';
    deleteBtn.innerHTML = '<i class="bx bx-trash text-danger"></i>';
    deleteBtn.title = 'Delete';
    deleteBtn.addEventListener('click', function () {
        uploadItems.removeChild(itemDiv);
        uploadedFiles = uploadedFiles.filter(f => f !== file); // Listeden çıkar
    });

    buttonsDiv.appendChild(deleteBtn);
    itemDiv.appendChild(fileName);
    itemDiv.appendChild(buttonsDiv);
    uploadItems.appendChild(itemDiv);
}

function initializeFormValidation() {
    const lcppvForm = document.getElementById('addLcppvForm');

    if (!lcppvForm) return;

    const fv = FormValidation.formValidation(lcppvForm, {
        fields: {
            ddlCompany: {
                validators: {
                    notEmpty: {
                        message: 'Please select a company'
                    }
                }
            },
            ddlCountry: {
                validators: {
                    notEmpty: {
                        message: 'Please select a country'
                    }
                }
            },
            ddlStatus: {
                validators: {
                    notEmpty: {
                        message: 'Please select a status'
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
            dateEnd: {
                validators: {
                    notEmpty: {
                        message: 'Please input end date'
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

        const company = document.getElementById('ddlCompany').value;
        const country = document.getElementById('ddlCountry').value;
        const status = document.getElementById('ddlStatus').value;

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
        const fltDue = document.querySelector('#dtDueDate')._flatpickr;
        const dueDate = fltDue.selectedDates[0];
        let isoDateDue;
        if (dueDate) {
            isoDateDue = new Date(dueDate.getTime() - dueDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }
        const comments = window.commentEditor.root.innerHTML;
        const userName = window.getUserName();
        if (!id) {
            const formData = {
                companyId: company, // bunu dinamik alacaksan değiştir
                countryId: country, // bunu da dinamik al
                statusId: status,
                startDate: isoDateStart,
                endDate: isoDateEnd, // örnek
                dueDate: isoDateDue,
                comment: comments,
                createdBy: userName, // oturumdan alınıyorsa güncelle
                answers: []
            };
            const questionContainers = document.querySelectorAll('#dvReportDetail .mb-4');
            questionContainers.forEach(container => {
                const questionText = container.querySelector('p')?.textContent.trim();
                const yesInput = container.querySelector('input[value="yes"]');
                const noInput = container.querySelector('input[value="no"]');

                const questionId = yesInput?.name; // name attribute = questionId

                if (yesInput?.checked || noInput?.checked) {
                    formData.answers.push({
                        questionId: questionId,
                        answer: yesInput.checked ? 1 : 2
                    });
                }
            });
            fetch(`${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/CreateLcppv`, {
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

                        UploadDocument(data.data);

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
                id:id,
                companyId: company, // bunu dinamik alacaksan değiştir
                countryId: country, // bunu da dinamik al
                statusId: status,
                startDate: isoDateStart,
                endDate: isoDateEnd, // örnek
                dueDate: isoDateDue,
                comment: comments,
                createdBy: userName, // oturumdan alınıyorsa güncelle
                answers: []
            };
            const questionContainers = document.querySelectorAll('#dvReportDetail .mb-4');
            questionContainers.forEach(container => {
                const questionText = container.querySelector('p')?.textContent.trim();
                const yesInput = container.querySelector('input[value="yes"]');
                const noInput = container.querySelector('input[value="no"]');

                const questionId = yesInput?.name; // name attribute = questionId

                if (yesInput?.checked || noInput?.checked) {
                    formData.answers.push({
                        questionId: questionId,
                        answer: yesInput.checked ? 1 : 2
                    });
                }
            });
            fetch(`${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/UpdateLcppv`, {
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

                        UpdateUploadDocument(data.data);

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

async function loadLcppv() {

    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;
    const response = await fetch(`${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/GetLcppvById/${id}`); // API adresin 
    const result = await response.json();
    const item = result.data;


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

    const dtDueDate = document.querySelector('#dtDueDate');
    dtDueDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtDueDate._flatpickr.setDate(item.dueDate, false);

    if (disableStatus==1) {

        $('#submitButton').prop('disabled', true);
        $('#ddlCompany').prop('disabled', true);
        $('#ddlCountry').prop('disabled', true);
        $('#ddlStatus').prop('disabled', true);

        dtStartDate._flatpickr.altInput.setAttribute('disabled', true);
        dtEndDate._flatpickr.altInput.setAttribute('disabled', true);
        dtDueDate._flatpickr.altInput.setAttribute('disabled', true);
        window.commentEditor.enable(false);
        document.querySelector('.ql-toolbar').classList.add('ql-disabled');
        document.getElementById('uploadIcon').classList.add('disabled');
        const uploadIcon = document.getElementById('uploadIcon');
        uploadIcon.removeAttribute('href');
    }
    $('#ddlCompany').val(item.companyId).trigger('change');
    $('#ddlCountry').val(item.countryId).trigger('change');
    $('#ddlStatus').val(item.statusId).trigger('change');

    window.commentEditor.root.innerHTML = item.comment;
    renderQuestions('dvReportDetail', item.questionsAndAnswers);
    displayUploadedFiles(item.documents);

}

function UploadDocument(LcppvId) {
    const formUploadData = new FormData();
    formUploadData.append('LcppvId', LcppvId);

    if (uploadedFiles.length > 0) {
        uploadedFiles.forEach(file => {
            formUploadData.append('Files', file); // API tarafında List<IFormFile> olarak karşılanacak
        });
    }
    fetch(`${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/CreateLcppvDocuments`, {
        method: 'POST',
        body: formUploadData
    })
        .then(response => response.json())
        .then(result => {

            if (result.data) {
                window.location.href = '/pv-system/lcppv';
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




}
function displayUploadedFiles(documents) {
    const uploadItems = document.getElementById('uploadItems');
    documents.forEach(doc => {
        console.log("doc.Id:", doc.id); // Bunu görebiliyor musun?
        documentsTemp.push({
            id: doc.id
        });
        const itemDiv = document.createElement('div');
        itemDiv.classList.add('d-flex', 'align-items-center', 'justify-content-between', 'mb-2', 'border', 'rounded', 'p-2');
        itemDiv.dataset.id = doc.Id;
        const fileName = document.createElement('span');
        fileName.textContent = doc.documentName;
        fileName.classList.add('text-truncate', 'me-2');

        const buttonsDiv = document.createElement('div');
        buttonsDiv.classList.add('d-flex', 'align-items-center');


        // İndirme butonu oluşturma
        const downloadBtn = document.createElement('a');
        downloadBtn.href = `${protocol}//${domain}:${port2}${doc.filePath}`;

        //downloadBtn.href = `C:/DitenPvOrganization/wwwroot/RegulatoryReport/${document.file}`; // Dosyanın doğru yolu
        downloadBtn.download = doc.documentName; // Dosya indirilirken adı
        downloadBtn.innerHTML = '<i class="bx bx-download text-success"></i>';
        downloadBtn.title = 'Download';
        downloadBtn.classList.add('me-2');

        // Silme butonu oluşturma
        const deleteBtn = document.createElement('a');
        deleteBtn.href = 'javascript:;';
        deleteBtn.innerHTML = '<i class="bx bx-trash text-danger"></i>';
        deleteBtn.title = 'Delete';
        deleteBtn.addEventListener('click', function () {
            uploadItems.removeChild(itemDiv);
            // Burada dosya silme işlemi yapabilirsiniz
            if (itemDiv && itemDiv.dataset && itemDiv.dataset.id) {
                
                const docId = itemDiv.dataset.id;
                console.log("silinendoc:", docId); // Bunu görebiliyor musun?
                const index = documentsTemp.findIndex(d => d.id == docId);
                if (index !== -1) {
                    documentsTemp.splice(index, 1);
                }
            }
        });

        buttonsDiv.appendChild(downloadBtn);
        buttonsDiv.appendChild(deleteBtn);
        itemDiv.appendChild(fileName);
        itemDiv.appendChild(buttonsDiv);
        uploadItems.appendChild(itemDiv);
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

function UpdateUploadDocument(LcppvId) {
    const formUploadData = new FormData();
    formUploadData.append('LcppvId', LcppvId);

    if (uploadedFiles.length > 0) {
        uploadedFiles.forEach(file => {
            formUploadData.append('Files', file); // API tarafında List<IFormFile> olarak karşılanacak
        });
    }
    if (documentsTemp.length>0) {

        documentsTemp.forEach((doc, index) => {
            formUploadData.append(`Documents[${index}].Id`, doc.id);
        });
    }
    fetch(`${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/UpdateLcppvDocuments`, {
        method: 'POST',
        body: formUploadData
    })
        .then(response => response.json())
        .then(result => {

            if (result.data) {
                window.location.href = '/pv-system/lcppv';
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




}

