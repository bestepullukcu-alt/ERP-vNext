'use strict';
dayjs.extend(dayjs_plugin_relativeTime);
let uploadedFiles = [];
let documentsTemp = [];
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const port2 = protocol === 'https:' ? '5055' : '5050';
const port3 = protocol === 'https:' ? '5060' : '5053';

const wizardIconsModern = document.querySelector('.wizard-modern-icons-example');

if (typeof wizardIconsModern !== undefined && wizardIconsModern !== null) {
    const wizardIconsModernBtnNextList = [].slice.call(wizardIconsModern.querySelectorAll('.btn-next')),
        wizardIconsModernBtnPrevList = [].slice.call(wizardIconsModern.querySelectorAll('.btn-prev')),
        wizardIconsModernBtnSubmit = wizardIconsModern.querySelector('.btn-submit');

    const modernIconsStepper = new Stepper(wizardIconsModern, {
        linear: false
    });

    if (wizardIconsModernBtnNextList) {
        wizardIconsModernBtnNextList.forEach(wizardIconsModernBtnNext => {
            wizardIconsModernBtnNext.addEventListener('click', event => {
                modernIconsStepper.next();
            });
        });
    }
    if (wizardIconsModernBtnPrevList) {
        wizardIconsModernBtnPrevList.forEach(wizardIconsModernBtnPrev => {
            wizardIconsModernBtnPrev.addEventListener('click', event => {
                modernIconsStepper.previous();
            });
        });
    }
    if (wizardIconsModernBtnSubmit) {
        wizardIconsModernBtnSubmit.addEventListener('click', event => {
            
            handleFormSubmit();
        });
    }
}
document.addEventListener('DOMContentLoaded', function () {
    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;


    const countryUrl = `${protocol}//${domain}:${port}/services/PvTenant/Tenant/GetCountriesByTenantId`;
    fetchCountries(countryUrl, "ddl-country");
    const statusUrl = `${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/GetSafetyReportStatus`;
    fetchDropdowns(statusUrl, "ddl-safety-report-status");

    const reportTypeUrl = `${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/GetSafetyReportTypes`;
    fetchDropdowns(reportTypeUrl, "ddl-report-type");

    const globalSkuUrl = `${protocol}//${domain}:${port}/services/PvOrganization/GlobalSku/GetGlobalSkus`;
    fetchGlobalSku(globalSkuUrl, "ddl-global-sku");

    const subjectRaceUrl = `${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/GetSubjectRace`;
    fetchDropdowns(subjectRaceUrl, "ddl-study-subject-race");

    const subjectInformationSourceUrl = `${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/GetSubjectInformationSource`;
    fetchDropdowns(subjectInformationSourceUrl, "ddl-source-information");

    const submissionStatusUrl = `${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/GetSafetySubmissionStatus`;
    fetchDropdowns(submissionStatusUrl, "ddl-safety-submission-status");

    const safetyDataTypeUrl = `${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/GetSafetyDataType`;
    fetchDropdowns(safetyDataTypeUrl, "ddl-safety-data-type");

    const causalityAssessmentUrl = `${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/GetCausalityAssessment`;
    fetchDropdowns(causalityAssessmentUrl, "ddl-causality-assessment");

    const authorityUrl = `${protocol}//${domain}:${port}/services/PvTenant/Authority/GetAuthoritiesByTenantId`;
    fetchGlobalSku(authorityUrl, "ddl-safety-submission-authority");

    const userUrl = `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByCompanyId`;
    fetchSafetyReportUsers(userUrl, ['ddl-assessment-by', 'ddl-assessor', 'ddl-review-by', 'ddl-reviewer', 'ddl-assigned-to','ddl-assigned']);

    const organizationUrl = `${protocol}//${domain}:${port}/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId`;
    fetchOrganizationList(organizationUrl, "ddl-assigned-organization");

    if (id) {
        loadSafetyReport();
    }



});
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

async function fetchGlobalSku(apiUrl, selectElementId) {
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
        const defaultOption = new Option("Select a global sku", "", false, false);
        selectElement.appendChild(defaultOption);

        let singleValue = null;

        data.data.forEach(globalSku => {
            const value = globalSku.id ??  globalSku.name;
            const option = new Option(globalSku.name, value, false, false);
            selectElement.appendChild(option);
        });

        // Eğer sadece 1 item varsa, onu otomatik olarak seç
        if (data.data.length === 1) {
            const singleItem = data.data[0];
            const selectedValue = singleItem.id ?? singleItem.name;
            $(selectElement).val(selectedValue);
        }

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Global sku alınırken hata oluştu:", error);
    }
}

async function fetchAuthorityList(apiUrl, selectElementId) {
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
        const defaultOption = new Option("Select a global sku", "", false, false);
        selectElement.appendChild(defaultOption);

        let singleValue = null;

        data.data.forEach(authority => {
            const value = authority.id ?? authority.name;
            const option = new Option(authority.name, value, false, false);
            selectElement.appendChild(option);
        });

        // Eğer sadece 1 item varsa, onu otomatik olarak seç
        if (data.data.length === 1) {
            const singleItem = data.data[0];
            const selectedValue = singleItem.id ?? singleItem.name;
            $(selectElement).val(selectedValue);
        }

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Global sku alınırken hata oluştu:", error);
    }
}

async function fetchOrganizationList(apiUrl, selectElementId) {
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
        const defaultOption = new Option("Select a organization", "", false, false);
        selectElement.appendChild(defaultOption);

        let singleValue = null;

        data.data.forEach(organization => {
            const value = organization.id ?? organization.companyName;
            const option = new Option(organization.companyName, value, false, false);
            selectElement.appendChild(option);
        });

        // Eğer sadece 1 item varsa, onu otomatik olarak seç
        if (data.data.length === 1) {
            const singleItem = data.data[0];
            const selectedValue = singleItem.id ?? singleItem.companyName;
            $(selectElement).val(selectedValue);
        }

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Organization alınırken hata oluştu:", error);
    }
}



async function fetchSafetyReportUsers(apiUrl, selectElementIds) {
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



function handleFormSubmit() {
const urlParams = new URLSearchParams(window.location.search);
const id = urlParams.get('id');
const disableStatus = urlParams.get('disabledStatus') ?? 0;

    const trackingNumber = document.getElementById('txt-tracking-number').value;
    const otherCaseIndentifier = document.getElementById('txt-other-case-indentifier').value;
    const safetyReportStatus = document.getElementById('ddl-safety-report-status').value;
    const country = document.getElementById('ddl-country').value;
    const globalSku = document.getElementById('ddl-global-sku').value;
    const literatureCase = document.getElementById('txt-literature-case').value;
    const reportType = document.getElementById('ddl-report-type').value;
    const isRelevantForSafetyEfficacyProfile = document.getElementById('chcIsRelevantForSafetyEfficacyProfile').checked;


    const fltReceived = document.querySelector('#dt-received')._flatpickr;
    const receivedDate = fltReceived.selectedDates[0];
    let isoDateReceived;
    if (receivedDate) {
        isoDateReceived = new Date(receivedDate.getTime() - receivedDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }

    const fltEntry = document.querySelector('#dt-entry')._flatpickr;
    const entryDate = fltEntry.selectedDates[0];
    let isoDateEntry;
    if (entryDate) {
        isoDateEntry = new Date(entryDate.getTime() - entryDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }

    const fltDue = document.querySelector('#dt-due')._flatpickr;
    const dueDate = fltDue.selectedDates[0];
    let isoDateDue;
    if (dueDate) {
        isoDateDue = new Date(dueDate.getTime() - dueDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }

    const literatureSource = document.getElementById('txt-literature-source').value;
    const article = document.getElementById('txt-article').value;
    const articleSource = document.getElementById('txt-article-source').value;
    const articleReference = document.getElementById('txt-article-reference').value;
    const literatureArticleCitation = document.getElementById('txt-literature-article-citation').value;
    const reporter = document.getElementById('txt-reporter').value;
    const patient = document.getElementById('ddl-patient').value;
    const subjectNumber = document.getElementById('txt-study-subject-number').value;
    const subjectRace = document.getElementById('ddl-study-subject-race').value;
    const subjectSite = document.getElementById('txt-study-subject-site').value;
    const subjectArm = document.getElementById('txt-study-subject-arm').value;
    const productDescription = document.getElementById('txt-product-description').value;
    const adverseReaction = document.getElementById('txt-adverse-reaction').value;
    const sourceInformation = document.getElementById('ddl-source-information').value;
    const isSubmissionRequired = document.getElementById('chcSubmissionRequired').checked;

    const fltSubmissionDue = document.querySelector('#dt-submission-due')._flatpickr;
    const submissionDueDate = fltSubmissionDue.selectedDates[0];
    let isoSubmissionDateDue;
    if (submissionDueDate) {
        isoSubmissionDateDue = new Date(submissionDueDate.getTime() - submissionDueDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }
    const summary = window.summaryEditor.root.innerHTML;

    const submissionStatus = document.getElementById('ddl-safety-submission-status').value;
    const authority = document.getElementById('ddl-safety-submission-authority').value;
    const dataType = document.getElementById('ddl-safety-data-type').value;
    const isSerious = document.getElementById('chcIsSerious').checked;
    const isUnexpected = document.getElementById('chcIsUnexpected').checked;
    const isInvalid = document.getElementById('chcIsInvalid').checked;
    const isDuplicate = document.getElementById('chcIsDuplicate').checked;

    const fltSubmissionDueMin = document.querySelector('#dt-safety-submission-due-min')._flatpickr;
    const submissionDueMinDate = fltSubmissionDueMin.selectedDates[0];
    let isoSubmissionDateDueMin;
    if (submissionDueMinDate) {
        isoSubmissionDateDueMin = new Date(submissionDueMinDate.getTime() - submissionDueMinDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }

    const fltSubmissionDueMax = document.querySelector('#dt-safety-submission-due-max')._flatpickr;
    const submissionDueMaxDate = fltSubmissionDueMax.selectedDates[0];
    let isoSubmissionDateDueMax;
    if (submissionDueMaxDate) {
        isoSubmissionDateDueMax = new Date(submissionDueMaxDate.getTime() - submissionDueMaxDate.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }

    const fltSubmissionDateMin = document.querySelector('#dt-safety-submission-min')._flatpickr;
    const submissionDateMin = fltSubmissionDateMin.selectedDates[0];
    let isoSubmissionDateMin;
    if (submissionDateMin) {
        isoSubmissionDateMin = new Date(submissionDateMin.getTime() - submissionDateMin.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }

    const fltSubmissionDateMax = document.querySelector('#dt-safety-submission-max')._flatpickr;
    const submissionDateMax = fltSubmissionDateMax.selectedDates[0];
    let isoSubmissionDateMax;
    if (submissionDateMax) {
        isoSubmissionDateMax = new Date(submissionDateMax.getTime() - submissionDateMax.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
    }

    const productComplaint = document.getElementById('txt-product-complaint').value;
    const medicalEnquiry = document.getElementById('txt-medical-enquiry').value;
    const severity = document.getElementById('txt-severity').value;
    const causalityAssessment = document.getElementById('ddl-causality-assessment').value;
    const causalityDescription = document.getElementById('txt-causality-description').value;
    const followUpTracker = document.getElementById('txt-followup-tracker').value;
    const assessmentBy = document.getElementById('ddl-assessment-by').value;
    const assessor = document.getElementById('ddl-assessor').value;
    const reviewBy = document.getElementById('ddl-review-by').value;
    const reviewer = document.getElementById('ddl-reviewer').value;
    const assignedTo = document.getElementById('ddl-assigned-to').value;
    const assigned = document.getElementById('ddl-assigned').value;
    const assignedOrganization = document.getElementById('ddl-assigned-organization').value;

    const safetyComment = window.safetyCommentEditor.root.innerHTML;
    const assessmentComment = window.assessmentCommentEditor.root.innerHTML;

    const userName = window.getUserName();

    if (id) {
        const formData = {
            id:id,
            trackingNumber: trackingNumber,
            otherCaseIdentifier: otherCaseIndentifier,
            safetyStatus: Number(safetyReportStatus),
            countryId: country,
            globalSkuId: globalSku,
            dateReceived: isoDateReceived,
            dateEntry: isoDateEntry,
            dateDue: isoDateDue,
            reportType: Number(reportType),
            literatureCase: literatureCase,
            isRelevantForSafetyEfficacyProfile: isRelevantForSafetyEfficacyProfile,
            article: article,
            articleSourceList: articleSource,
            articleReference: articleReference,
            literatureArticleCitation: literatureArticleCitation,
            summary: summary,
            literatureSource: literatureSource,
            reporter: reporter,
            patientId: patient,
            subjectNumber: subjectNumber,
            subjectRace: Number(subjectRace),
            subjectStudySite: subjectSite,
            subjectArm: subjectArm,
            productDescription: productDescription,
            adverseReaction: adverseReaction,
            informationSource: Number(sourceInformation),
            submissionRequired: isSubmissionRequired,
            submissionDueDate: isoSubmissionDateDue,
            safetySubmissionStatus: Number(submissionStatus),
            safetySubmissionDueDateMin: isoSubmissionDateDueMin,
            safetySubmissionDueDateMax: isoSubmissionDateDueMax,
            safetySubmissionDateMin: isoSubmissionDateMin,
            safetySubmissionDateMax: isoSubmissionDateMax,
            submissionAuthorityId: authority,
            safetyDataType: Number(dataType),
            isSerious: isSerious,
            isUnexpected: isUnexpected,
            isInvalid: isInvalid,
            isDuplicate: isDuplicate,
            productQualityComplaintDescription: productComplaint,
            medicalInformationEnquiry: medicalEnquiry,
            severityDescription: severity,
            causalityAssessment: Number(causalityAssessment),
            causalityDescription: causalityDescription,
            safetyComments: safetyComment,
            followUpTracker: followUpTracker,
            assessmentBy: assessmentBy,
            assessorId: assessor,
            reviewedBy: reviewBy,
            reviewer: reviewer,
            assignedTo: assignedTo,
            assigned: assigned,
            assignedOrganizationId: assignedOrganization,
            comment: assessmentComment,
            createdBy: userName,
        };
        fetch(`http://${domain}:5000/services/PvOrganization/SafetyReport/UpdateSafetyReport`, {
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
    else {
        const formData = {
            trackingNumber: trackingNumber,
            otherCaseIdentifier: otherCaseIndentifier, 
            safetyStatus: Number(safetyReportStatus),
            countryId: country,
            globalSkuId: globalSku,
            dateReceived: isoDateReceived,
            dateEntry: isoDateEntry,
            dateDue: isoDateDue,
            reportType: Number(reportType),
            literatureCase: literatureCase,
            isRelevantForSafetyEfficacyProfile: isRelevantForSafetyEfficacyProfile,
            article: article,
            articleSourceList: articleSource,
            articleReference: articleReference,
            literatureArticleCitation: literatureArticleCitation,
            summary: summary,
            literatureSource: literatureSource,
            reporter: reporter,
            patientId: patient,
            subjectNumber: subjectNumber,
            subjectRace: Number(subjectRace),
            subjectStudySite: subjectSite,
            subjectArm: subjectArm,
            productDescription: productDescription,
            adverseReaction: adverseReaction,
            informationSource: Number(sourceInformation),
            submissionRequired: isSubmissionRequired,
            submissionDueDate: isoSubmissionDateDue,
            safetySubmissionStatus: Number(submissionStatus),
            safetySubmissionDueDateMin: isoSubmissionDateDueMin,
            safetySubmissionDueDateMax: isoSubmissionDateDueMax,
            safetySubmissionDateMin: isoSubmissionDateMin,
            safetySubmissionDateMax: isoSubmissionDateMax,
            submissionAuthorityId: authority,
            safetyDataType: Number(dataType),
            isSerious: isSerious,
            isUnexpected: isUnexpected,
            isInvalid: isInvalid,
            isDuplicate: isDuplicate,
            productQualityComplaintDescription: productComplaint,
            medicalInformationEnquiry: medicalEnquiry,
            severityDescription: severity,
            causalityAssessment: Number(causalityAssessment),
            causalityDescription: causalityDescription,
            safetyComments: safetyComment,
            followUpTracker: followUpTracker,
            assessmentBy: assessmentBy,
            assessorId: assessor,
            reviewedBy: reviewBy,
            reviewer: reviewer,
            assignedTo: assignedTo,
            assigned: assigned,
            assignedOrganizationId: assignedOrganization,
            comment: assessmentComment,
            createdBy: userName, 
        };
        fetch(`${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/CreateSafetyReport`, {
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



}

function UploadDocument(SafetyReportId) {
    const formUploadData = new FormData();
    formUploadData.append('SafetyReportId', SafetyReportId);

    if (uploadedFiles.length > 0) {
        uploadedFiles.forEach(file => {
            formUploadData.append('Files', file); // API tarafında List<IFormFile> olarak karşılanacak
        });
    }
    fetch(`${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/CreateSafetyReportDocuments`, {
        method: 'POST',
        body: formUploadData
    })
        .then(response => response.json())
        .then(result => {

            if (result.data) {
                window.location.href = '/pv-system/safety-report';
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

async function loadSafetyReport() {

    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const disableStatus = urlParams.get('disabledStatus') ?? 0;
    const response = await fetch(`${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/GetSafetyReportById/${id}`); // API adresin 
    const result = await response.json();
    const item = result.data;

    $('#txt-tracking-number').val(item.trackingNumber);
    $('#txt-other-case-indentifier').val(item.otherCaseIdentifier);
    $('#ddl-safety-report-status').val(item.safetyStatus).trigger('change');
    $('#ddl-country').val(item.countryId).trigger('change');
    $('#ddl-global-sku').val(item.globalSkuId).trigger('change');
    $('#ddl-report-type').val(item.reportType).trigger('change');
    $('#txt-literature-case').val(item.literatureCase);
    document.getElementById('chcIsRelevantForSafetyEfficacyProfile').checked = item.isRelevantForSafetyEfficacyProfile;
    window.summaryEditor.root.innerHTML = item.summary;
    $('#txt-literature-source').val(item.literatureSource);
    $('#txt-article').val(item.article);
    $('#txt-article').val(item.article);
    $('#txt-article-source').val(item.articleSourceList);
    $('#txt-article-reference').val(item.articleReference);
    $('#txt-literature-article-citation').val(item.literatureArticleCitation);
    $('#txt-reporter').val(item.reporter);
    $('#ddl-patient').val(item.patientId).trigger('change');
    $('#txt-study-subject-number').val(item.subjectNumber);
    $('#ddl-study-subject-race').val(item.subjectRace).trigger('change');
    $('#txt-study-subject-site').val(item.subjectStudySite);
    $('#txt-study-subject-arm').val(item.subjectArm);
    $('#txt-product-description').val(item.productDescription);
    $('#txt-adverse-reaction').val(item.adverseReaction);
    $('#ddl-source-information').val(item.informationSource).trigger('change');
    document.getElementById('chcSubmissionRequired').checked = item.submissionRequired;
    $('#ddl-safety-submission-status').val(item.safetySubmissionStatus).trigger('change');
    $('#ddl-safety-submission-authority').val(item.submissionAuthorityId).trigger('change');
    $('#ddl-safety-data-type').val(item.safetyDataType).trigger('change');
    document.getElementById('chcIsSerious').checked = item.isSerious;
    document.getElementById('chcIsUnexpected').checked = item.isUnexpected;
    document.getElementById('chcIsInvalid').checked = item.isInvalid;
    document.getElementById('chcIsDuplicate').checked = item.isDuplicate;
    $('#txt-product-complaint').val(item.productQualityComplaintDescription);
    $('#txt-medical-enquiry').val(item.medicalInformationEnquiry);
    $('#txt-severity').val(item.severityDescription);
    $('#ddl-causality-assessment').val(item.causalityAssessment).trigger('change');
    $('#txt-causality-description').val(item.causalityDescription);
    window.safetyCommentEditor.root.innerHTML = item.safetyComments;
    $('#txt-followup-tracker').val(item.followUpTracker);
    $('#ddl-assessment-by').val(item.assessmentBy).trigger('change');
    $('#ddl-assessor').val(item.assessorId).trigger('change');
    $('#ddl-review-by').val(item.reviewedBy).trigger('change');
    $('#ddl-reviewer').val(item.reviewer).trigger('change');
    $('#ddl-assigned-to').val(item.assignedTo).trigger('change');
    $('#ddl-assigned').val(item.assigned).trigger('change');
    $('#ddl-assigned-organization').val(item.assignedOrganizationId).trigger('change');
    window.assessmentCommentEditor.root.innerHTML = item.comment;

    const dtSubmissionDueMaxDate = document.querySelector('#dt-safety-submission-due-max');
    dtSubmissionDueMaxDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtSubmissionDueMaxDate._flatpickr.setDate(item.safetySubmissionDueDateMax, false);

    const dtSubmissionMaxDate = document.querySelector('#dt-safety-submission-max');
    dtSubmissionMaxDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtSubmissionMaxDate._flatpickr.setDate(item.safetySubmissionDateMax, false);


    const dtSubmissionMinDate = document.querySelector('#dt-safety-submission-min');
    dtSubmissionMinDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtSubmissionMinDate._flatpickr.setDate(item.safetySubmissionDateMin, false);


    const dtSubmissionDueMinDate = document.querySelector('#dt-safety-submission-due-min');
    dtSubmissionDueMinDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtSubmissionDueMinDate._flatpickr.setDate(item.safetySubmissionDueDateMin, false);

    const dtSubmissionDueDate = document.querySelector('#dt-submission-due');
    dtSubmissionDueDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtSubmissionDueDate._flatpickr.setDate(item.submissionDueDate, false);

    const dtReceivedDate = document.querySelector('#dt-received');
    dtReceivedDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtReceivedDate._flatpickr.setDate(item.dateReceived, false);

    const dtEntryDate = document.querySelector('#dt-entry');
    dtEntryDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtEntryDate._flatpickr.setDate(item.dateEntry, false);

    const dtDueDate = document.querySelector('#dt-due');
    dtDueDate.flatpickr({
        altInput: true,
        altFormat: 'd.m.Y',
        dateFormat: 'Y-m-d',
        static: true
    });
    dtDueDate._flatpickr.setDate(item.dateDue, false);







    if (disableStatus == 1) {

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

    //
    //renderQuestions('dvReportDetail', item.questionsAndAnswers);
    displayUploadedFiles(item.documents);

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
        downloadBtn.href = `${protocol}//${domain}:${port3}${doc.filePath}`;

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

function UpdateUploadDocument(SafetyId) {
    const formUploadData = new FormData();
    formUploadData.append('SafetyId', SafetyId);

    if (uploadedFiles.length > 0) {
        uploadedFiles.forEach(file => {
            formUploadData.append('Files', file); // API tarafında List<IFormFile> olarak karşılanacak
        });
    }
    if (documentsTemp.length > 0) {

        documentsTemp.forEach((doc, index) => {
            formUploadData.append(`Documents[${index}].Id`, doc.id);
        });
    }
    fetch(`${protocol}//${domain}:${port}/services/PvOrganization/SafetyReport/UpdateSafetyDocuments`, {
        method: 'POST',
        body: formUploadData
    })
        .then(response => response.json())
        .then(result => {

            if (result.data) {
                window.location.href = '/pv-system/safety-report';
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


