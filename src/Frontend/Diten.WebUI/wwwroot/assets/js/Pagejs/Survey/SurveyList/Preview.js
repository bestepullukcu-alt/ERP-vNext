'use strict'
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const url = new URL(window.location.href);
const pathParts = url.pathname.split('/');
const surveyId = pathParts[2]; // "test"
const userName = window.getUserName();
let surveyQuestions = [];
let currentStep = 1;
// Soruları tutacak global değişken

document.addEventListener('DOMContentLoaded', function () {

    GetSurveyInformation(surveyId);
    loadSurvey(surveyId);
});

$(document).on('click', '#exitBtnLink', function () {
    window.location.href = `/survey/survey-list?filterSurveyId=${surveyId}`;
});


async function GetSurveyInformation(templateIdOrId) {
    try {
        const response = await fetch(`${protocol}//${domain}:${port}/services/PvSurvey/Survey/GetSurveyById?id=${templateIdOrId}`);
        if (!response.ok) throw new Error("API error");
        const data = await response.json();
        const surveyTitle = data?.data?.name || "Untitled Survey";
        const description = data?.data?.description || "";
        //const surveyTypeId = data?.data?.surveyType?.id || "";
        //const targetAudienceId = data?.data?.targetAudience?.id || 0;
        //const languageId = data?.data?.language?.id || "";
        const duration = data?.data?.duration || 0;

        $('#survey-title').text(surveyTitle);
        $('#survey-description').text(description);
        $('#survey-duration').html(`<i class="bx bx-timer"></i> ${duration} min`);        //$('#txt-duration').val(duration);




    } catch (err) {
        console.error(err);
    }
}


async function loadSurvey(templateIdOrId) {
    try {
        const response = await fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveyDesign/GetSurveyDesignBySurveyId?id=${templateIdOrId}`);
        if (!response.ok) throw new Error("API error");
        const data = await response.json();
        const rawQuestions = data?.data?.questions || [];

        // Total steps
        const totalSteps = rawQuestions.length;
        const currentStep = 1;

        // UI güncelle
        $('#total-steps').text(totalSteps);
        $('#current-step').text(currentStep);

        // Progress bar yüzdesi
        const progressPercent = totalSteps > 0 ? Math.round((currentStep / totalSteps) * 100) : 0;

        $('#progress-percent').text(`${progressPercent}%`);
        $('#survey-progress')
            .css('width', `${progressPercent}%`)
            .attr('aria-valuenow', progressPercent);

        // Soruları global değişkende tut (ileride Next/Prev için)
        surveyQuestions = rawQuestions;

        renderQuestion(currentStep);
        updateProgress();

    } catch (err) {
        console.error(err);
        surveyQuestions = []; // boş template
        $('#survey-container').html('<p class="text-danger">Failed to load survey questions.</p>');
    }
}

// 🔹 Soru render etme fonksiyonu
function renderQuestion(step) {
    const question = surveyQuestions[step - 1];
    if (!question) return;

    const questionContainer = $('#question-body');
    questionContainer.empty();

    // 🔹 Required işareti
    const requiredMark = question.isRequired ? '<span class="text-danger ms-1">*</span>' : '';


    let html = `<h5 class="mb-3">${question.title || 'Untitled Question'} ${requiredMark}</h5>`;

    switch (question.type) {
        case 'short-text':
            html += `<input type="text" class="form-control answer-input" placeholder="Enter your answer...">`;
            break;

        case 'long-text':
            html += `<textarea class="form-control answer-input" rows="3" placeholder="Enter your answer..."></textarea>`;
            break;

        case 'single-choice':
            html += question.options.map((opt, i) => `
            <div class="form-check mb-2">
                <input class="form-check-input answer-input" type="radio" name="q${question.id}" id="opt${i}">
                <label class="form-check-label" for="opt${i}">${opt.value}</label>
            </div>
        `).join('');
            break;

        case 'multiple-choice':
            html += question.options.map((opt, i) => `
            <div class="form-check mb-2">
                <input class="form-check-input answer-input" type="checkbox" name="q${question.id}" id="opt${i}">
                <label class="form-check-label" for="opt${i}">${opt.value}</label>
            </div>
        `).join('');
            break;

        case 'dropdown':
            html += `
        <select class="form-select answer-input">
            <option value="">Select an option</option>
            ${question.options.map(opt => `<option value="${opt.value}">${opt.value}</option>`).join('')}
        </select>
        `;
            break;
        case 'rating-scale':
            html += `
        <div class="rating-scale-container d-flex justify-content-center gap-2">
            ${[1, 2, 3, 4, 5].map(v => `
                <button type="button"
                    class="btn btn-outline-primary rating-btn answer-input"
                    data-value="${v}">
                    ${v}
                </button>
            `).join('')}
        </div>
        `;
            break;
        case 'email':
            html += `
                <input type="email" class="form-control answer-input" placeholder="example@email.com">
            `;
            break;
        case 'phone':
            html += `
                <input type="tel" class="form-control answer-input" placeholder="+90 5xx xxx xx xx"
                       pattern="^\\+?\\d{1,4}?\\s?\\(?\\d{1,3}?\\)?[-.\\s]?\\d{3,4}[-.\\s]?\\d{3,4}$">
            `;
            break;

        default:
            html += `<p class="text-muted">Unsupported question type</p>`;
    }


    // 🔹 Hata mesajı alanı (başta gizli)
    html += `
        <div class="alert alert-danger alert-dismissible mt-3 d-none" role="alert">
            <h6 class="alert-heading d-flex align-items-center flex-wrap gap-1 mb-2">
                <span class="alert-icon rounded-circle"><i class="icon-base bx bx-error"></i></span>
                Error!
            </h6>
            <p>Please answer this required question before continuing.</p>
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `;

    questionContainer.html(html);

    // 🔹 Buton durumlarını güncelle
    $('#prev-btn').prop('disabled', step === 1);
    $('#next-btn').text(step === surveyQuestions.length ? 'Save' : 'Next');

    // 🔹 İlerleme çubuğunu güncelle (isteğe bağlı)
    $('#current-step').text(step);
    $('#total-steps').text(surveyQuestions.length);
    const progress = (step / surveyQuestions.length) * 100;
    $('#survey-progress').css('width', `${progress}%`);
    $('#progress-percent').text(`${Math.round(progress)}%`);

    renderPager(step);
}

// returns { ok: boolean, message?: string }
// 🔹 Validation (hata mesajını kendisi gösterir)
function validateCurrentQuestion() {
    const question = surveyQuestions[currentStep - 1];
    if (!question) return { ok: true };

    const $container = $('#question-body');
    const alertBox = $container.find('.alert-danger');
    const inputs = $container.find('.answer-input');

    alertBox.addClass('d-none');
    alertBox.find('p').text('');

    if (!question.isRequired) return { ok: true };

    let answered = false;
    let regexValid = true;

    switch (question.type) {
        case 'short-text':
        case 'long-text':
            const textVal = inputs.first().val()?.trim() || '';
            answered = textVal !== '';
            break;

        case 'email':
            const emailVal = inputs.first().val()?.trim() || '';
            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            answered = emailVal !== '' && emailRegex.test(emailVal);
            if (emailVal !== '' && !emailRegex.test(emailVal)) regexValid = false;
            break;

        case 'phone':
            const phoneVal = inputs.first().val()?.trim() || '';
            const phoneRegex = /^\+?\d{1,4}?\s?\(?\d{1,3}?\)?[-.\s]?\d{3,4}[-.\s]?\d{3,4}$/;
            answered = phoneVal !== '' && phoneRegex.test(phoneVal);
            if (phoneVal !== '' && !phoneRegex.test(phoneVal)) regexValid = false;
            break;

        case 'single-choice':
        case 'multiple-choice':
            answered = inputs.filter((_, el) => $(el).is(':checked')).length > 0;
            break;

        case 'dropdown':
            const selVal = inputs.first().val();
            answered = selVal !== undefined && selVal !== '';
            break;

        case 'rating-scale':
            const $ratingContainer = $container.find('.rating-scale-container');
            const dataSel = $ratingContainer.data('selectedValue');
            const hasActive = $ratingContainer.find('.rating-btn.active, .rating-btn.selected').length > 0;
            answered = (dataSel !== undefined && dataSel !== null) || hasActive;
            break;
    }

    if (!answered) {
        alertBox.removeClass('d-none');
        alertBox.find('p').text('Lütfen bu zorunlu soruyu yanıtlayın.');
        return { ok: false };
    }

    if (!regexValid) {
        alertBox.removeClass('d-none');
        alertBox.find('p').text('Girilen değer geçerli formatta değil.');
        return { ok: false };
    }

    return { ok: true };
}

// 🔹 Buton eventleri
$('#next-btn').on('click', function () {
    if (!validateCurrentQuestion().ok) return;
    if (currentStep < surveyQuestions.length) {
        currentStep++;
        renderQuestion(currentStep);
        updatePager();
    }
});

$('#prev-btn').on('click', function () {
    if (currentStep > 1) {
        currentStep--;
        renderQuestion(currentStep);
        updatePager();
    }
});

// 🔹 Pager (1,2,3 numaralı sayfalar)
function updatePager() {
    const pager = $('#survey-pager');
    pager.empty();

    for (let i = 1; i <= surveyQuestions.length; i++) {
        const btn = $('<button>')
            .addClass('btn btn-sm me-1')
            .addClass(i === currentStep ? 'btn-primary' : 'btn-outline-primary')
            .text(i)
            .on('click', () => {
                // sadece valid ise sayfa geçişine izin ver
                if (!validateCurrentQuestion().ok) return;
                currentStep = i;
                renderQuestion(currentStep);
                updatePager();
            });

        pager.append(btn);
    }
}

// 🔹 Progress güncelleme
function updateProgress() {
    const total = surveyQuestions.length;
    const percent = total > 0 ? Math.round((currentStep / total) * 100) : 0;

    $('#current-step').text(currentStep);
    $('#progress-percent').text(`${percent}%`);
    $('#survey-progress')
        .css('width', `${percent}%`)
        .attr('aria-valuenow', percent);
}


// 🔹 Pager (Sayfa numaraları)
function renderPager(currentStepArg) {
    const pager = $('#survey-pager');
    pager.empty();
    const totalSteps = surveyQuestions.length;

    for (let i = 1; i <= totalSteps; i++) {
        pager.append(`
            <li class="page-item ${i === currentStepArg ? 'active' : ''}">
                <a class="page-link" href="javascript:void(0);" data-step="${i}">${i}</a>
            </li>
        `);
    }

    pager.off('click', '.page-link'); // önceki handler'ı temizle
    pager.on('click', '.page-link', function () {
        const selectedStep = parseInt($(this).data('step'), 10);
        if (selectedStep === currentStep) return;

        // Önce mevcut soruyu validate et
        const res = validateCurrentQuestion();
        if (!res.ok) {
            const $alert = $('#question-body').find('.alert-danger');
            $alert.removeClass('d-none').find('p').first().text(res.message);
            return;
        }

        // gizle alert ve geçiş
        $('#question-body').find('.alert-danger').addClass('d-none');

        currentStep = selectedStep;
        renderQuestion(currentStep);
    });
}




document.addEventListener("click", function (e) {
    if (e.target.classList.contains("rating-btn")) {
        const container = e.target.closest(".rating-scale-container");

        // Tüm butonlardan active sınıfını kaldır
        container.querySelectorAll(".rating-btn").forEach(btn => {
            btn.classList.remove("btn-primary");
            btn.classList.add("btn-outline-primary");
        });

        // Tıklanan butonu aktif hale getir
        e.target.classList.remove("btn-outline-primary");
        e.target.classList.add("btn-primary");

        // Seçilen değeri container'a kaydet
        container.dataset.selectedValue = e.target.dataset.value;
    }
});