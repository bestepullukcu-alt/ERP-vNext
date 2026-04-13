'use strict'
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const leftPanel = document.getElementById("leftPanel");
const formBuilder = document.getElementById("formBuilder");
let questionCount = 0;
let surveyQuestions = [];
const url = new URL(window.location.href);
const pathParts = url.pathname.split('/');
const surveyId = pathParts[2]; // "test"
// 2️⃣ "templateId" parametresini almak
const templateId = url.searchParams.get('templateId'); // "blank"
const userName = window.getUserName();
Dropzone.autoDiscover = false;
const surveyBreadcrumb = document.getElementById('surveyNameBreadcrumb');
console.log(Dropzone.version);

$(document).on('click', '#surveyListLink', function () {
    window.location.href = `/survey/survey-list?filterSurveyId=${surveyId}`;
});

document.addEventListener('DOMContentLoaded', function () {

    GetSurveyInformation(surveyId);

    if (templateId === 'blank') {


        loadSurvey(surveyId);
    }
    else { loadSurvey(templateId); }



});





// Div e tab-content class ını ekle
document.addEventListener('shown.bs.tab', function (event) {
    const activatedTab = event.target;
    const tabContentWrapper = document.querySelector('.nav-align-top > div');
    const formBuilder = document.querySelector('#formBuilder');

    if (activatedTab.getAttribute('data-bs-target') === '#navs-pills-top-settings') {
        tabContentWrapper.classList.add('tab-content');
        formBuilder.classList.add('d-none'); // sadece gizle
    } else {
        tabContentWrapper.classList.remove('tab-content');
        formBuilder.classList.remove('d-none'); // tekrar göster
    }
});

// Div e tab-content class ını ekle




// Sol paneldeki tıklamaları yakala
leftPanel.addEventListener("click", (e) => {
    const btn = e.target.closest(".list-group-item");
    if (!btn || btn.disabled) return; // 🔹 disable ise tıklamayı engelle

    const type = btn.dataset.type;
    addQuestion(type);
});

// Ana yönlendirici fonksiyon
function addFormElement(type, questionIndex) {
    questionCount++;

    let card;
    switch (type) {
        case "short-text":
            card = createShortTextCard(type,questionIndex);
            break;
        case "long-text":
            card = createLongTextCard(type, questionIndex);
            break;
        case "date":
            card = createDateCard(type, questionIndex);
            break;
        case "time":
            card = createTimeCard(type, questionIndex);
            break;
        case "single-choice": 
            card = createMultipleChoiceSingleCard(type, questionIndex);
            break;
        case "multiple-choice":
            card = createMultipleChoiceMultiCard(type, questionIndex);
            break;
        case "dropdown":
            card = createDropdownCard(type, questionIndex);
            break;
        case "matrix":
            card = createMatrixCard(type, questionIndex);
            break;
        case "slider-scale":
            card = createSliderScaleCard(type, questionIndex);
            break;
        case "rating-scale":
            card = createRatingScaleCard(type, questionIndex);
            break;
        case "email":
            card = createEmailCard(type, questionIndex);
            break;
        case "phone":
            card = createPhoneNumberCard(type, questionIndex);
            break;
        case "address":
            card = createAddressCard(type, questionIndex);
            break;
        case "website-url":
            card = createWebsiteUrlCard(type, questionIndex);
            break;
        case "image-upload":
            card = createImageUploadCard(type, questionIndex);
            break;
        case "file-upload":
            card = createFileUploadCard(type, questionIndex);
            break;
        case "video":
            card = createVideoUploadCard(type, questionIndex);
            break;
        case "number":
            card = createNumberCard(type, questionIndex);
            break;
        case "numeric-range":
            card = createNumericRangeCard(type, questionIndex);
            break;
        default:
            console.warn("Unknown type:", type);
            return;
    }

    formBuilder.appendChild(card);
    updateQuestionNumbers();

    return card;
}

// 🧱 Ortak kart yapısını oluşturan yardımcı fonksiyon
function createBaseCard(type, contentHtml) {
    const card = document.createElement("div");
    card.className = "card mb-3 p-3 shadow-sm";
    card.innerHTML = `
    <div class="d-flex justify-content-between align-items-center mb-2">
      <div>
        <span class="badge bg-label-primary me-2 question-number"></span>
        <span class="badge bg-label-secondary">${type}</span>
      </div>
      <div>
        <button class="btn btn-sm btn-icon btn-outline-secondary move-up" title="Move Up"><i class="bx bx-up-arrow-alt"></i></button>
        <button class="btn btn-sm btn-icon btn-outline-secondary move-down" title="Move Down"><i class="bx bx-down-arrow-alt"></i></button>
        <button class="btn btn-sm btn-icon btn-outline-secondary copy" title="Copy"><i class="bx bx-copy"></i></button>
        <button class="btn btn-sm btn-icon btn-outline-danger delete" title="Delete"><i class="bx bx-trash"></i></button>
      </div>
    </div>
    ${contentHtml}
  `;

    // 🗑️ Delete
    card.querySelector(".delete").addEventListener("click", () => {

        // 1. Array'den sil
        const index = surveyQuestions.findIndex(q => q.order == card.dataset.order);
        if (index !== -1) {
            surveyQuestions.splice(index, 1);
        }

        card.remove();

        // 3. Sıraları güncelle
        surveyQuestions.forEach((q, i) => q.order = i); // order tekrar düzenleniyor

        updateQuestionNumbers();
    });

    // ⬆️ Move up
    card.querySelector(".move-up").addEventListener("click", () => {

        const currentOrder = parseInt(card.dataset.order);
        const index = surveyQuestions.findIndex(q => q.order === currentOrder);
     
        if (index > 0) {
            // 1. Array'de swap
            [surveyQuestions[index - 1], surveyQuestions[index]] = [surveyQuestions[index], surveyQuestions[index - 1]];

            // 2. DOM'da swap
            const prev = card.previousElementSibling;
            if (prev) formBuilder.insertBefore(card, prev);

            // 3. order güncelle
            surveyQuestions.forEach((q, i) => q.order = i);
            updateQuestionNumbers();
        }
    });

    // ⬇️ Move down
    card.querySelector(".move-down").addEventListener("click", () => {

        
        const currentOrder = parseInt(card.dataset.order);
        const index = surveyQuestions.findIndex(q => q.order === currentOrder);

        
        //const index = surveyQuestions.findIndex(q => q.order == card.dataset.order);
        if (index < surveyQuestions.length - 1) {
            // 1. Array'de swap
            [surveyQuestions[index], surveyQuestions[index + 1]] = [surveyQuestions[index + 1], surveyQuestions[index]];

            // 2. DOM'da swap
            const next = card.nextElementSibling;
            if (next) formBuilder.insertBefore(next, card);

            // 3. order güncelle
            surveyQuestions.forEach((q, i) => q.order = i);
            updateQuestionNumbers();
        }
    });

    return card;
}

// 🧮 Sıralama numaralarını güncelle
function updateQuestionNumbers() {
    const cards = formBuilder.querySelectorAll(".card");
    cards.forEach((card, index) => {
        const badge = card.querySelector(".question-number");
        if (badge) badge.textContent = `Question ${index + 1}`;
        card.dataset.order = index;
    });
}

// 📝 Short Text elemanı
function createShortTextCard(type,questionIndex) {
    const contentHtml = `
<div class="d-flex gap-4 mb-3">
  <div class="form-check form-switch">
    <input class="form-check-input quizMode-toggle" type="checkbox" id="quizMode${questionCount}">
    <label class="form-check-label" for="quizMode${questionCount}">Quiz mode</label>
  </div>

  <div class="form-check form-switch">
    <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
    <label class="form-check-label" for="required${questionCount}">Required</label>
  </div>
</div>

    <div class="mb-3">
      <label class="form-label">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="mb-3 quiz-answer" style="display: none;">
      <label class="form-label text-success">Correct Answer (text)</label>
      <input type="text" class="form-control correctAnswer-toggle" placeholder="Enter the expected correct answer">
    </div>

    <div class="disabled-div">
      <label class="form-label text-muted">Preview</label>
      <input type="text" class="form-control" placeholder="Enter your answer..." disabled>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);
    // Quiz mode toggle
    const quizToggle = card.querySelector(`#quizMode${questionCount}`);
    const requiredToggle = card.querySelector(`#required${questionCount}`);
    const quizAnswer = card.querySelector(".quiz-answer");
    const correctAnswer = card.querySelector(".correctAnswer-toggle");

    const question = surveyQuestions.find(q => q.order === questionIndex);

   
    requiredToggle.addEventListener("change", (e) => {
        if (question) {
            question.isRequired = e.target.checked;           // array’e kaydet
        }
    });


    quizToggle.addEventListener("change", (ev) => {
        quizAnswer.style.display = ev.target.checked ? "block" : "none";
        if (question) {
            question.isQuizMode = ev.target.checked;           // array’e kaydet
        }

    });

    correctAnswer.addEventListener("input", (e) => {
        if (question) {
            question.correctAnswer = e.target.value;        // array’e kaydet
        }
    });


    return card;
}

// 🧾 Long Text elemanı
function createLongTextCard(type, questionIndex) {
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
  <div class="form-check form-switch">
    <input class="form-check-input quizMode-toggle" type="checkbox" id="quizMode${questionCount}">
    <label class="form-check-label" for="quizMode${questionCount}">Quiz mode</label>
  </div>

  <div class="form-check form-switch">
    <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
    <label class="form-check-label" for="required${questionCount}">Required</label>
  </div>
</div>

    <div class="mb-3">
      <label class="form-label">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

     <div class="mb-3 quiz-answer" style="display: none;">
      <label class="form-label text-success">Correct Answer (text)</label>
      <input type="text" class="form-control correctAnswer-toggle" placeholder="Enter the expected correct answer">
    </div>

    <div class="disabled-div">
      <label class="form-label text-muted">Preview</label>
      <textarea class="form-control" rows="3" placeholder="Enter your answer..." disabled></textarea>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    const question = surveyQuestions.find(q => q.order === questionIndex);
    // Quiz mode toggle
    const quizToggle = card.querySelector(`#quizMode${questionCount}`);
    const requiredToggle = card.querySelector(`#required${questionCount}`);
    const quizAnswer = card.querySelector(".quiz-answer");
    const correctAnswer = card.querySelector(".correctAnswer-toggle");

   

    correctAnswer.addEventListener("input", (e) => {
        if (question) {
            question.correctAnswer = e.target.value;        // array’e kaydet
        }
    });


    requiredToggle.addEventListener("change", (e) => {
        
        if (question) {
            question.isRequired = e.target.checked;           // array’e kaydet
        }
    });

    quizToggle.addEventListener("change", (ev) => {


        quizAnswer.style.display = ev.target.checked ? "block" : "none";

        if (question) {
            question.isQuizMode = e.target.checked;           // array’e kaydet
        }


    });

    return card;


}


// date elemanı
function createDateCard(type, questionIndex) {
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
  <div class="form-check form-switch">
    <input class="form-check-input quizMode-toggle" type="checkbox" id="quizMode${questionCount}">
    <label class="form-check-label" for="quizMode${questionCount}">Quiz mode</label>
  </div>

  <div class="form-check form-switch">
    <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
    <label class="form-check-label" for="required${questionCount}">Required</label>
  </div>
</div>

    <div class="mb-3">
      <label class="form-label">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

     <div class="mb-3 quiz-answer" style="display: none;">
      <label class="form-label text-success">Correct Answer (text)</label>
      <input type="text" class="form-control correct-answer-input" placeholder="DD,Month, YYYY">
    </div>

    <div class="disabled-div">
      <label class="form-label text-muted">Preview</label>
      <input type="text" class="form-control preview-date-input" placeholder="DD,Month, YYYY" disabled>

    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    // Quiz mode toggle
    const quizToggle = card.querySelector(`#quizMode${questionCount}`);
    const requiredToggle = card.querySelector(`#required${questionCount}`);
    const quizAnswer = card.querySelector(".quiz-answer");
    const question = surveyQuestions.find(q => q.order === questionIndex);

    quizToggle.addEventListener("change", (ev) => {
        quizAnswer.style.display = ev.target.checked ? "block" : "none";

        if (question) {
            question.isQuizMode = ev.target.checked;           // array’e kaydet
        }
    });

    requiredToggle.addEventListener("change", (e) => {

        if (question) {
            question.isRequired = e.target.checked;           // array’e kaydet
        }
    });

    // ✅ Flatpickr initialization
    const correctAnswerInput = card.querySelector(".correct-answer-input");
    const previewDateInput = card.querySelector(".preview-date-input");

    if (correctAnswerInput) {
        flatpickr(correctAnswerInput, {
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true,
            onChange: function (selectedDates, dateStr) {
                // Array ile senkronizasyon
                const order = parseInt(card.dataset.order); // card ile question objesini eşleştir
                const question = surveyQuestions.find(q => q.order === order);
                if (question) {
                    question.correctAnswer = dateStr;       // array'e kaydet
                }

                // Preview input'u güncelle
                const previewDateInput = card.querySelector(".preview-date-input");
                if (previewDateInput) {
                    previewDateInput._flatpickr.setDate(dateStr, true); // flatpickr update
                }
            }
        });
    }

    if (previewDateInput) {
        flatpickr(previewDateInput, {
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }


    return card;


}

// time elemanı
function createTimeCard(type, questionIndex) {
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
  <div class="form-check form-switch">
    <input class="form-check-input quizMode-toggle" type="checkbox" id="quizMode${questionCount}">
    <label class="form-check-label" for="quizMode${questionCount}">Quiz mode</label>
  </div>

  <div class="form-check form-switch">
    <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
    <label class="form-check-label" for="required${questionCount}">Required</label>
  </div>
</div>

    <div class="mb-3">
      <label class="form-label">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

     <div class="mb-3 quiz-answer" style="display:none;">
      <label class="form-label text-success">Correct Answer (text)</label>
     <input type="text" class="form-control correct-answer-time" placeholder="HH:MM" />
    </div>

    <div class="disabled-div">
      <label class="form-label text-muted">Preview</label>
      <input type="text" class="form-control preview-date-time" placeholder="HH:MM" disabled>

    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    // Quiz mode toggle
    const quizToggle = card.querySelector(`#quizMode${questionCount}`);
    const quizAnswer = card.querySelector(".quiz-answer");
    const requiredToggle = card.querySelector(`#required${questionCount}`);
    const question = surveyQuestions.find(q => q.order === questionIndex);

    quizToggle.addEventListener("change", (ev) => {
        quizAnswer.style.display = ev.target.checked ? "block" : "none";
        if (question) {
            question.isQuizMode = ev.target.checked;           // array’e kaydet
        }
    });
    requiredToggle.addEventListener("change", (e) => {

        if (question) {
            question.isRequired = e.target.checked;           // array’e kaydet
        }
    });
    // ✅ Flatpickr initialization
    const correctAnswerTime = card.querySelector(".correct-answer-time");
    const previewDateTime = card.querySelector(".preview-date-time");

    if (correctAnswerTime) {
        flatpickr(correctAnswerTime, {
            enableTime: true,
            noCalendar: true,
            static: true,
            onChange: function (selectedDates, timeStr) {
                // Array ile senkronizasyon
                const order = parseInt(card.dataset.order);
                const question = surveyQuestions.find(q => q.order === order);
                if (question) {
                    question.correctAnswer = timeStr; // array'e kaydet
                }

                // Preview input'u güncelle
                if (previewDateTime) {
                    previewDateTime._flatpickr.setDate(timeStr, true); // flatpickr update
                }
            }
        });
    }

    if (previewDateTime) {
        flatpickr(previewDateTime, {
            enableTime: true,
            noCalendar: true,
            static: true
        });
    }


    return card;


}

// single-choice elemanı
function createMultipleChoiceSingleCard(type, questionIndex) {
    
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
      <div class="form-check form-switch">
        <input class="form-check-input quizMode-toggle" type="checkbox" id="quizMode${questionCount}">
        <label class="form-check-label" for="quizMode${questionCount}">Quiz mode</label>
      </div>

      <div class="form-check form-switch">
        <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
        <label class="form-check-label" for="required${questionCount}">Required</label>
      </div>
    </div>

    <div class="mb-3">
      <label class="form-label">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="options-container mb-3"></div>
    <button type="button" class="btn btn-sm btn-outline-primary add-option-btn mb-3">
      + Add Option
    </button>

    <div class="disabled-div">
      <label class="form-label text-muted">Preview</label>
      <div class="preview-area border rounded p-2 bg-light"></div>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    const quizToggle = card.querySelector(`#quizMode${questionCount}`);
    const requiredToggle = card.querySelector(`#required${questionCount}`);
    const optionsContainer = card.querySelector(".options-container");
    const previewArea = card.querySelector(".preview-area");
    const addOptionBtn = card.querySelector(".add-option-btn");
    const question = surveyQuestions.find(q => q.order === questionIndex);

    let options = question.options;

    const renderOptions = () => {
        optionsContainer.innerHTML = "";
        previewArea.innerHTML = "";
        options.forEach((opt, i) => {
            
            const optRow = document.createElement("div");
            optRow.className = "d-flex align-items-center gap-2 mb-2";

            optRow.innerHTML = `
                <input type="text" class="form-control option-text" value="${opt.value}" placeholder="Option ${i + 1}">
<div class="form-check ms-2">
                <input type="radio" name="correct${questionCount}" class="form-check-input correct-radio align-middle" style="margin-top: 0.35rem" ${opt.isCorrect ? "checked" : ""} ${!quizToggle.checked ? "disabled" : ""}>
                <label class="form-check-label text-muted small align-middle" style="margin-top: 0.35rem">Correct</label>
</div>
        <button class="btn btn-sm btn-outline-danger remove-option"><i class="bx bx-trash"></i></button>
        
      `;

            // Correct seçimi
            optRow.querySelector(".correct-radio").addEventListener("change", () => {
                options.forEach(o => (o.isCorrect = false));
                options[i].isCorrect = true;
                if (question) question.options = [...options]; // array'i güncelle
                renderPreview();
            });

            // Option text değişimi
            optRow.querySelector(".option-text").addEventListener("input", e => {
                options[i].value = e.target.value;
                if (question) question.options = [...options]; // array'i güncelle
                renderPreview();
            });

            // Option silme
            optRow.querySelector(".remove-option").addEventListener("click", () => {
                if (options.length === 1) return alert("En az bir option olmalıdır!");
                options.splice(i, 1);
                if (question) question.options = [...options]; // array'i güncelle
                renderOptions();
            });
            optionsContainer.appendChild(optRow);
        });

        renderPreview();
    };

    const renderPreview = () => {
        previewArea.innerHTML = options
            .map(
                (opt, i) => `
        <div class="form-check">
          <input class="form-check-input" type="radio" name="preview-${questionCount}" id="p${i}" ${opt.isCorrect ? "checked" : ""} disabled>
          <label class="form-check-label" for="p${i}">${opt.value || `Option ${i + 1}`}</label>
        </div>`
            )
            .join("");
    };

    requiredToggle.addEventListener("change", (e) => {
        if (question) {
            question.isRequired = e.target.checked;           // array’e kaydet
        }
    });


    addOptionBtn.addEventListener("click", () => {
        options.push({ value: `Option ${options.length + 1}`, isCorrect: false, order: options.length + 1 });
        // Array ile senkronizasyon
        const order = parseInt(card.dataset.order);
        const question = surveyQuestions.find(q => q.order === order);
        if (question) question.options = [...options];

        renderOptions();

        // Quiz mode açık ve hiç correct yoksa -> ilkini correct yap
        if (quizToggle.checked && !options.some(o => o.correct)) {
            options[0].correct = true;

            // Array tekrar güncelleniyor
            if (question) question.options = [...options];

            renderOptions();
        }
    });

    quizToggle.addEventListener("change", (ev) => {
        const isQuiz = ev.target.checked;

        // Quiz mode açık değilse correct seçimleri temizle ve disable yap
        if (!isQuiz) {
            options.forEach(o => (o.isCorrect = false));
        } else if (options.length > 0 && !options.some(o => o.isCorrect)) {
            // Quiz mode açıldıysa ve correct yoksa otomatik ilkini correct yap
            options[0].isCorrect = true;
        }


        // Array ile senkronizasyon
        const question = surveyQuestions.find(q => q.order === questionIndex);
        if (question) {
            question.options = [...options];
            question.isQuizMode = isQuiz;
        }

        renderOptions();
    });

    // Array ile senkronizasyon
    //if (question) question.options = [...options];


    return card;
}

// multiple-choice elemanı

function createMultipleChoiceMultiCard(type, questionIndex) {
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
      <div class="form-check form-switch">
        <input class="form-check-input quizMode-toggle" type="checkbox" id="quizMode${questionCount}">
        <label class="form-check-label" for="quizMode${questionCount}">Quiz mode</label>
      </div>

      <div class="form-check form-switch">
        <input class="form-check-input" type="checkbox" id="required${questionCount}">
        <label class="form-check-label required-toggle" for="required${questionCount}">Required</label>
      </div>
    </div>

    <div class="mb-3">
      <label class="form-label">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="options-container mb-3"></div>
    <button type="button" class="btn btn-sm btn-outline-primary add-option-btn mb-3">
      + Add Option
    </button>

    <div class="disabled-div">
      <label class="form-label text-muted">Preview</label>
      <div class="preview-area border rounded p-2 bg-light"></div>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    // card oluşturulduktan sonra array ile eşleştir
    const question = surveyQuestions.find(q => q.order === questionIndex);


    const quizToggle = card.querySelector(`#quizMode${questionCount}`);
    const requiredToggle = card.querySelector(`#required${questionCount}`);
    const optionsContainer = card.querySelector(".options-container");
    const previewArea = card.querySelector(".preview-area");
    const addOptionBtn = card.querySelector(".add-option-btn");

    let options = [];
    if (question) options = question ? [...question.options] : [];


    const renderOptions = () => {
        optionsContainer.innerHTML = "";
        previewArea.innerHTML = "";

        options.forEach((opt, i) => {
            const optRow = document.createElement("div");
            optRow.className = "d-flex align-items-center gap-2 mb-2";

            optRow.innerHTML = `
                <input type="text" class="form-control option-text" value="${opt.value}" placeholder="Option ${i + 1}">
                <div class="form-check ms-2">
                    <input type="checkbox" class="form-check-input correct-checkbox align-middle" style="margin-top: 0.35rem"
                        ${opt.isCorrect ? "checked" : ""} ${!quizToggle.checked ? "disabled" : ""}>
                    <label class="form-check-label text-muted small align-middle" style="margin-top: 0.35rem">Correct</label>
                </div>
                <button class="btn btn-sm btn-outline-danger remove-option">
                    <i class="bx bx-trash"></i>
                </button>
            `;

            

            // Correct checkbox değişimi
            optRow.querySelector(".correct-checkbox").addEventListener("change", e => {
                options[i].isCorrect = e.target.checked;
                if (question) question.options = [...options];
                renderPreview();
            });

            // Option text değişimi
            optRow.querySelector(".option-text").addEventListener("input", e => {
                options[i].value = e.target.value;
                if (question) question.options = [...options];
                renderPreview();
            });


            // Option silme
            optRow.querySelector(".remove-option").addEventListener("click", () => {
                if (options.length === 1) return alert("En az bir option olmalıdır!");
                options.splice(i, 1);
                if (question) question.options = [...options];
                renderOptions();
            });
            optionsContainer.appendChild(optRow);
        });

        renderPreview();
    };

    const renderPreview = () => {
        previewArea.innerHTML = options
            .map(
                (opt, i) => `
        <div class="form-check">
          <input class="form-check-input" type="checkbox" id="p${i}" ${opt.isCorrect ? "checked" : ""} disabled>
          <label class="form-check-label" for="p${i}">${opt.value || `Option ${i + 1}`}</label>
        </div>`
            )
            .join("");
    };

    // Option ekleme
    addOptionBtn.addEventListener("click", () => {
        options.push({ value: `Option ${options.length + 1}`, isCorrect: false, order: options.length + 1  });

        // Array ile senkron
        if (question) question.options = [...options];

        renderOptions();
    });

    requiredToggle.addEventListener("change", (e) => {
        if (question) {
            question.isRequired = e.target.checked;           // array’e kaydet
        }
    });



    quizToggle.addEventListener("change", (ev) => {
        const isQuiz = ev.target.checked;
        if (question) {
            question.isQuizMode = isQuiz;           // array’e kaydet
        }

        if (!isQuiz) {
            // Quiz mode kapalıyken correct'leri sıfırla
            options.forEach(o => (o.isCorrect = false));
        }
        renderOptions();
    });

    if (question && (!question.options || question.options.length === 0)) {
        // Başlangıçta quiz mode kapalı, correct'ler false
        options.push({ value: "Option 1", isCorrect: false, order: 1 });
        options.push({ value: "Option 2", isCorrect: false, order: 2 });
    }
    if (question) question.options = [...options];
    renderOptions();

    return card;
}

// dropdown elemanı
function createDropdownCard(type, questionIndex) {
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
        <div class="form-check form-switch">
            <input class="form-check-input quizMode-toggle" type="checkbox" id="quizMode${questionCount}">
            <label class="form-check-label" for="quizMode${questionCount}">Quiz mode</label>
        </div>
        <div class="form-check form-switch">
            <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
            <label class="form-check-label" for="required${questionCount}">Required</label>
        </div>
    </div>
    <div class="mb-3">
        <label class="form-label">Question Title</label>
        <input type="text" class="form-control question-title" placeholder="New Question">
    </div>
    <div class="options-container mb-3"></div>
    <button type="button" class="btn btn-sm btn-outline-primary add-option-btn mb-3">+ Add Option</button>
    <div class="disabled-div">
        <label class="form-label text-muted">Preview</label>
        <select class="form-select preview-area" disabled></select>
    </div>
    `;

    const card = createBaseCard(type, contentHtml);
    const quizToggle = card.querySelector(`#quizMode${questionCount}`);
    const optionsContainer = card.querySelector(".options-container");
    const previewArea = card.querySelector(".preview-area");
    const addOptionBtn = card.querySelector(".add-option-btn");
    const question = surveyQuestions.find(q => q.order === questionIndex);

    let options = [];
    if (question) options = question ? [...question.options] : [];


    // Array ile eşleştirme

    // Title input
    card.querySelector(".question-title").addEventListener("input", (e) => {
        if (question) question.title = e.target.value;
    });

    // Required toggle
    card.querySelector(".required-toggle").addEventListener("change", (e) => {
        if (question) question.isRequired = e.target.checked;
    });

    
    const renderOptions = () => {
        optionsContainer.innerHTML = "";

        options.forEach((opt, i) => {
            const optRow = document.createElement("div");
            optRow.className = "d-flex align-items-center gap-2 mb-2";

            optRow.innerHTML = `
                <input type="text" class="form-control option-text" value="${opt.value}" placeholder="Option ${i + 1}">
                <div class="form-check">
                    <input class="form-check-input correct-radio" type="radio" name="correct${questionCount}" ${opt.isCorrect ? "checked" : ""} ${quizToggle.checked ? "" : "disabled"}>
                    <label class="form-check-label small text-muted">Correct</label>
                </div>
                <button class="btn btn-sm btn-outline-danger remove-option"><i class="bx bx-trash"></i></button>
            `;

            // Option text değişimi
            optRow.querySelector(".option-text").addEventListener("input", e => {
                options[i].value = e.target.value;
                if (question) question.options = [...options];
                renderPreview();
            });

            // Correct radio change
            optRow.querySelector(".correct-radio").addEventListener("change", e => {
                options.forEach(o => o.isCorrect = false);
                options[i].isCorrect = true;
                if (question) question.options = [...options];
                renderPreview();
            });

            // Option silme
            optRow.querySelector(".remove-option").addEventListener("click", () => {
                const wasCorrect = options[i].isCorrect;
                options.splice(i, 1);
                if (wasCorrect && quizToggle.checked && options.length > 0) {
                    options[0].isCorrect = true;
                }
                if (question) question.options = [...options];
                renderOptions();
            });

            optionsContainer.appendChild(optRow);
        });

        renderPreview();
    };

    const renderPreview = () => {
        previewArea.innerHTML = "";
        options.forEach((opt, i) => {
            const optionEl = document.createElement("option");
            optionEl.value = i;
            optionEl.textContent = opt.value || `Option ${i + 1}`;
            if (opt.correct) optionEl.selected = true;
            previewArea.appendChild(optionEl);
        });
    };

    // Option ekleme
    addOptionBtn.addEventListener("click", () => {
        options.push({ value: `Option ${options.length + 1}`, isCorrect: false, order: options.length + 1 });

        if (quizToggle.checked && !options.some(o => o.isCorrect)) {
            options[0].isCorrect = true;
        }

        if (question) question.options = [...options];
        renderOptions();
    });

    // Quiz mode toggle
    quizToggle.addEventListener("change", ev => {
        const isQuiz = ev.target.checked;
        question.isQuizMode = isQuiz;

        if (!isQuiz) {
            options.forEach(o => o.isCorrect = false);
        } else if (options.length > 0 && !options.some(o => o.isCorrect)) {
            options[0].isCorrect = true;
        }

        if (question) question.options = [...options];
        renderOptions();
    });

    if (question && (!question.options || question.options.length === 0)) {
        // Başlangıçta 1 option ekle
        options.push({ value: "Option 1", isCorrect: false, order: 1 });
        if (quizToggle.checked) options[0].isCorrect = true;
    }
    renderOptions();

    return card;
}

function createMatrixCard(type, questionIndex) {
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
        <div class="form-check form-switch">
            <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
            <label class="form-check-label" for="required${questionCount}">Required</label>
        </div>
    </div>

    <div class="mb-3">
        <label class="form-label">Question Title</label>
        <input type="text" class="form-control question-title" placeholder="Enter question title">
    </div>

    <!-- ROWS -->
    <div class="mb-3">
        <label class="form-label">Rows</label>
        <div class="rows-container mb-2"></div>
        <button type="button" class="btn btn-sm btn-outline-primary add-row-btn">+ Add Row</button>
    </div>

    <!-- COLUMNS -->
    <div class="mb-3">
        <label class="form-label">Columns</label>
        <div class="cols-container mb-2"></div>
        <button type="button" class="btn btn-sm btn-outline-primary add-col-btn">+ Add Column</button>
    </div>

    <!-- PREVIEW -->
    <div class="disabled-div">
        <label class="form-label text-muted">Preview</label>
        <div class="preview-area border rounded p-2 bg-light overflow-auto"></div>
    </div>
    `;

    const card = createBaseCard(type, contentHtml);

    const rowsContainer = card.querySelector(".rows-container");
    const colsContainer = card.querySelector(".cols-container");
    const previewArea = card.querySelector(".preview-area");
    const addRowBtn = card.querySelector(".add-row-btn");
    const addColBtn = card.querySelector(".add-col-btn");
    const requiredToggle = card.querySelector(".required-toggle");
    const titleInput = card.querySelector(".question-title");

    // Array ile eşleştirme
    const question = surveyQuestions.find(q => q.order === questionIndex);

    let rows = [];
    let cols = [];

    // Title input
    titleInput.addEventListener("input", e => {
        if (question) question.title = e.target.value;
    });

    // Required toggle
    requiredToggle.addEventListener("change", e => {
        if (question) question.isRequired = e.target.checked;
    });


    /** RENDER ROWS **/
    const renderRows = () => {
        rowsContainer.innerHTML = "";
        rows.forEach((r, i) => {
            const rowDiv = document.createElement("div");
            rowDiv.className = "d-flex align-items-center gap-2 mb-2";
            rowDiv.innerHTML = `
                <input type="text" class="form-control row-text" value="${r}" placeholder="Row ${i + 1}">
                <button class="btn btn-sm btn-outline-danger remove-row"><i class="bx bx-trash"></i></button>
            `;
            // row text change
            rowDiv.querySelector(".row-text").addEventListener("input", e => {
                rows[i] = e.target.value;
                updateQuestionRowsCols();
                renderPreview();
            });
            // row remove
            rowDiv.querySelector(".remove-row").addEventListener("click", () => {
                if (rows.length === 1) {
                    alert("En az bir satır olmalıdır!");
                    return;
                }
                rows.splice(i, 1);
                updateQuestionRowsCols();
                renderRows();
            });
            rowsContainer.appendChild(rowDiv);
        });
        renderPreview();
    };

    /** RENDER COLUMNS **/
    const renderCols = () => {
        colsContainer.innerHTML = "";
        cols.forEach((c, i) => {
            const colDiv = document.createElement("div");
            colDiv.className = "d-flex align-items-center gap-2 mb-2";
            colDiv.innerHTML = `
                <input type="text" class="form-control col-text" value="${c}" placeholder="Column ${i + 1}">
                <button class="btn btn-sm btn-outline-danger remove-col"><i class="bx bx-trash"></i></button>
            `;
            // col text change
            colDiv.querySelector(".col-text").addEventListener("input", e => {
                cols[i] = e.target.value;
                updateQuestionRowsCols();
                renderPreview();
            });
            // col remove
            colDiv.querySelector(".remove-col").addEventListener("click", () => {
                if (cols.length === 1) {
                    alert("En az bir sütun olmalıdır!");
                    return;
                }
                cols.splice(i, 1);
                updateQuestionRowsCols();
                renderCols();
            });
            colsContainer.appendChild(colDiv);
        });
        renderPreview();
    };

    const updateQuestionRowsCols = () => {
        if (question) {
            question.matrixRows = [...rows];
            question.matrixColumns = [...cols];
        }
    };



    /** RENDER PREVIEW TABLE **/
    const renderPreview = () => {
        if (rows.length === 0 || cols.length === 0) {
            previewArea.innerHTML = `<p class="text-muted small mb-0">En az 1 satır ve 1 sütun ekleyiniz.</p>`;
            return;
        }

        let tableHtml = `<table class="table table-bordered align-middle mb-0">
            <thead>
                <tr>
                    <th></th>
                    ${cols.map(c => `<th class="text-center">${c || "Column"}</th>`).join("")}
                </tr>
            </thead>
            <tbody>
                ${rows
                .map(
                    (r, ri) => `
                    <tr>
                        <th>${r || "Row"}</th>
                        ${cols
                            .map(
                                (c, ci) =>
                                    `<td class="text-center">
                                        <input type="radio" name="matrix${questionCount}_row${ri}" class="form-check-input">
                                    </td>`
                            )
                            .join("")}
                    </tr>`
                )
                .join("")}
            </tbody>
        </table>`;

        previewArea.innerHTML = tableHtml;
    };

    /** ADD ROW / COLUMN EVENTS **/
    addRowBtn.addEventListener("click", () => {
        rows.push(`Row ${rows.length + 1}`);
        updateQuestionRowsCols();
        renderRows();
    });

    addColBtn.addEventListener("click", () => {
        cols.push(`Column ${cols.length + 1}`);
        updateQuestionRowsCols();
        renderCols();
    });

    if (question) {
        if (!question.matrixRows || question.matrixRows.length === 0) {
            rows = ["Row 1"];
            question.matrixRows = rows;
        } else {
            rows = question.matrixRows;
        }

        if (!question.matrixColumns || question.matrixColumns.length === 0) {
            cols = ["Column 1"];
            question.matrixColumns = cols;
        } else {
            cols = question.matrixColumns;
        }
    }
    // Başlangıçta 1 satır 1 sütun
    updateQuestionRowsCols();
    renderRows();
    renderCols();

    return card;
}

function createSliderScaleCard(type, questionIndex) {
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
      <div class="form-check form-switch">
        <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
        <label class="form-check-label" for="required${questionCount}">Required</label>
      </div>
    </div>

    <div class="row mb-3">
      <div class="col">
        <label class="form-label">Min Value</label>
        <input type="number" class="form-control min-value" value="1" min="0">
      </div>
      <div class="col">
        <label class="form-label">Max Value</label>
        <input type="number" class="form-control max-value" value="10" min="1">
      </div>
    </div>

    <div class="mb-3">
      <label class="form-label">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="mb-3">
      <label class="form-label">Step</label>
      <input type="number" class="form-control step-value" value="1" min="1">
    </div>

    <div class="p-3 rounded disabled-div">
      <label class="form-label text-muted d-block">Preview</label>
      <div class="d-flex justify-content-between mb-1">
        <span class="min-label">1</span>
        <span class="max-label">10</span>
      </div>
      <input type="range" class="form-range slider-preview" min="1" max="10" step="1" value="5">
      <div class="text-center fw-semibold preview-value">5</div>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    // Referansları al
    const minInput = card.querySelector(".min-value");
    const maxInput = card.querySelector(".max-value");
    const stepInput = card.querySelector(".step-value");
    const slider = card.querySelector(".slider-preview");
    const previewValue = card.querySelector(".preview-value");
    const minLabel = card.querySelector(".min-label");
    const maxLabel = card.querySelector(".max-label");

    const requiredToggle = card.querySelector(".required-toggle");
    const titleInput = card.querySelector(".question-title");

    // her zaman güncel question almak için helper
    function getQuestion() {
        return surveyQuestions.find(q => q.order === questionIndex);
    }

    function toIntSafe(v, fallback) {
        const n = Number(v);
        return Number.isFinite(n) ? Math.trunc(n) : fallback;
    }

    // updateSlider: güvenli parse, clamp, ve modele yaz
    function updateSlider() {
        const min = toIntSafe(minInput.value, 1);
        const max = toIntSafe(maxInput.value, min + 1);
        const step = Math.max(1, toIntSafe(stepInput.value, 1));

        // ensure min < max
        let adjMax = max;
        if (min >= adjMax) {
            adjMax = min + step;
            maxInput.value = adjMax;
        }

        slider.min = min;
        slider.max = adjMax;
        slider.step = step;

            // başlangıçta orta değeri al veya mevcut slider.value kullan
            const mid = Math.floor((min + adjMax) / 2);
            slider.value = Math.max(min, Math.min(mid, adjMax));
            
        

        // clamp slider
        let sVal = toIntSafe(slider.value, min);
        if (sVal < min) sVal = min;
        if (sVal > adjMax) sVal = adjMax;
        slider.value = sVal;

        previewValue.textContent = slider.value;
        minLabel.textContent = min;
        maxLabel.textContent = adjMax;

        const question = getQuestion();
        if (question) {
            // güvenli parse
            const min = Number.isFinite(Number(question.minValue ?? question.minLabel)) ? Number(question.minValue ?? question.minLabel) : 1;
            const max = Number.isFinite(Number(question.maxValue ?? question.maxLabel)) ? Number(question.maxValue ?? question.maxLabel) : 10;
            const step = Number.isFinite(Number(question.step)) ? Number(question.step) : 1;
            const sliderVal = Number.isFinite(Number(question.sliderValue)) ? Number(question.sliderValue) : Math.floor((min + max) / 2);

            // input’ları set et
            if (minInput) minInput.value = min;
            if (maxInput) maxInput.value = max;
            if (stepInput) stepInput.value = step;
            if (slider) slider.value = sliderVal;
            if (previewValue) previewValue.textContent = sliderVal;
            if (minLabel) minLabel.textContent = min;
            if (maxLabel) maxLabel.textContent = max;

            // diğer alanlar
            if (titleInput) titleInput.value = question.title ?? "";
            if (requiredToggle) requiredToggle.checked = !!question.isRequired;
        } else {
            // Eğer question henüz yoksa, console log ile gözetle; debugger ile bekletme yerine buradan takip edebilirsin
            console.warn("updateSlider: question not found yet for index", questionIndex);
        }
    }

    // event bağlamaları
    titleInput.addEventListener("input", () => {
        const question = getQuestion();
        if (question) question.title = titleInput.value;
    });

    requiredToggle.addEventListener("change", () => {
        const question = getQuestion();
        if (question) question.isRequired = requiredToggle.checked;
    });

    slider.addEventListener("input", () => {
        previewValue.textContent = slider.value;
        const question = getQuestion();
        if (question) question.sliderValue = toIntSafe(slider.value, question.sliderValue || 0);
    });

    // input + change ikisini bağla (tarayıcı/UX farkları için)
    ["input", "change"].forEach(evt => {
        minInput.addEventListener(evt, () => {
            const question = getQuestion();
            if (question) question.minLabel = toIntSafe(minInput.value, question.minLabel || 1);
            updateSlider();
        });

        maxInput.addEventListener(evt, () => {
            const question = getQuestion();
            if (question) question.maxLabel = toIntSafe(maxInput.value, question.maxLabel || 10);
            updateSlider();
        });

        stepInput.addEventListener(evt, () => {
            const question = getQuestion();
            if (question) question.step = Math.max(1, toIntSafe(stepInput.value, question.step || 1));
            updateSlider();
        });
    });

    // **Çok önemli**: başlangıçta bir kere çalıştır — böylece debugger olmadan da doğru set olur.
    // Bazı durumlarda question henüz yoksa updateSlider hata vermez ama model güncellenmez; bu yüzden,
    // eğer question yoksa bir kere daha (küçük bir timeout ile) dene — bu, debugger'ın yerine geçen kontrollü bekleme.
    updateSlider();
    if (!getQuestion()) {
        // Eğer ilk anda question yoksa, kısa bir bekleme ile tekrar dene (100ms). Bu debugger gibi uzun bekletmez, sadece kısa bir retry sağlar.
        setTimeout(() => {
            updateSlider();
            if (!getQuestion()) {
                console.warn("createSliderScaleCard: question still missing after short retry for index", questionIndex);
            }
        }, 80);
    }

    return card;
}

function createRatingScaleCard(type, questionIndex) {
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
      <div class="form-check form-switch">
        <input class="form-check-input quizMode-toggle" type="checkbox" id="quizMode${questionCount}">
        <label class="form-check-label" for="quizMode${questionCount}">Quiz mode</label>
      </div>

      <div class="form-check form-switch">
        <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
        <label class="form-check-label" for="required${questionCount}">Required</label>
      </div>
    </div>

    <div class="mb-3">
      <label class="form-label">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="mb-3">
      <label class="form-label">Correct Rating</label>
      <select class="form-select correct-rating" disabled>
        <option value="">Select rating</option>
        ${[1, 2, 3, 4, 5].map(v => `<option value="${v}">${v}</option>`).join("")}
      </select>
    </div>

    <div class="p-3 disabled-div">
      <label class="form-label text-muted d-block">Preview</label>

      <ul class="pagination pagination-sm">
      ${[1, 2, 3, 4, 5].map(v => `

      <li class="page-item">
      <a name="rating-preview-${questionCount}" class="page-link" href="javascript:void(0);">${v}</a>
      </li>

        `).join("")}

       </ul>

    </div>
  `;

    const card = createBaseCard(type, contentHtml);


    function updatePreview() {
        // Önce question'ı al
        const question = surveyQuestions.find(q => q.order === questionIndex);
        if (!question) return;

        // Correct answer değerini al
        const correctVal = question.correctAnswer;

        // Her bir rating butonunu kontrol et
        const previewButtons = card.querySelectorAll(`[name="rating-preview-${questionCount}"]`);

        previewButtons.forEach(btn => {
            const btnVal = btn.textContent.trim();

            // Seçilen değerse vurgula, değilse temizle
            if (correctVal && btnVal === String(correctVal)) {
                btn.classList.add("active");
                btn.classList.add("bg-primary", "text-white");
            } else {
                btn.classList.remove("active", "bg-primary", "text-white");
            }
        });
    }





    // Element referansları
    const quizToggle = card.querySelector(`#quizMode${questionCount}`);
    const requiredToggle = card.querySelector(".required-toggle");
    const titleInput = card.querySelector(".question-title");
    const correctRatingSelect = card.querySelector(".correct-rating");
    const previewItems = card.querySelectorAll(".rating-preview .page-link");

    // Array ile senkron
    const question = surveyQuestions.find(q => q.order === questionIndex);
    //if (question) {
    //    question.title = titleInput.value;
    //    question.isRequired = requiredToggle.checked;
    //    question.correctAnswer = null;
    //}

    // Title input
    titleInput.addEventListener("input", e => {
        if (question) question.title = e.target.value;
    });

    // Required toggle
    requiredToggle.addEventListener("change", e => {
        if (question) question.isRequired = e.target.checked;
    });

    // Quiz mode toggle
    quizToggle.addEventListener("change", e => {
        const isQuiz = e.target.checked;
        correctRatingSelect.disabled = !isQuiz;
        if (question) question.isQuizMode = e.target.checked;

        if (!isQuiz && question) {
            question.correctAnswer = null;
            correctRatingSelect.value = "";
            updatePreview();
        }
    });

    // Correct rating seçilince preview’da işaretleme gösterimi
    // Correct rating seçimi
    correctRatingSelect.addEventListener("change", e => {
        const val = e.target.value;
        if (question) question.correctAnswer = val === "" ? null : val;
        updatePreview();
    });

    return card;
}

function createEmailCard(type, questionIndex) {
    const contentHtml = `
    <div class="form-check form-switch">
      <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
      <label class="form-check-label" for="required${questionCount}">Required</label>
    </div>

    <div class="mb-3 mt-2">
      <label class="form-label fw-semibold">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="p-3 disabled-div">
      <label class="form-label text-muted d-block">Preview</label>
      <input type="email" class="form-control" placeholder="example@email.com" disabled>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    const requiredToggle = card.querySelector(".required-toggle");
    const titleInput = card.querySelector(".question-title");
    // Array ile senkron
    const question = surveyQuestions.find(q => q.order === questionIndex);

    // Title input
    titleInput.addEventListener("input", e => {
        if (question) question.title = e.target.value;
    });

    // Required toggle
    requiredToggle.addEventListener("change", e => {
        if (question) question.isRequired = e.target.checked;
    });


    return card;
}

function createPhoneNumberCard(type, questionIndex) {
    const contentHtml = `
    <div class="form-check form-switch">
      <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
      <label class="form-check-label" for="required${questionCount}">Required</label>
    </div>

    <div class="mb-3 mt-2">
      <label class="form-label fw-semibold">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="p-3 disabled-div">
      <label class="form-label text-muted d-block">Preview</label>
      <input type="tel" class="form-control" placeholder="+90 5xx xxx xx xx" disabled>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);
    const requiredToggle = card.querySelector(".required-toggle");
    const titleInput = card.querySelector(".question-title");
    // Array ile senkron
    const question = surveyQuestions.find(q => q.order === questionIndex);

    // Title input
    titleInput.addEventListener("input", e => {
        if (question) question.title = e.target.value;
    });

    // Required toggle
    requiredToggle.addEventListener("change", e => {
        if (question) question.isRequired = e.target.checked;
    });
    return card;
}

function createAddressCard(type, questionIndex) {
    const contentHtml = `
    <div class="form-check form-switch">
      <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
      <label class="form-check-label" for="required${questionCount}">Required</label>
    </div>

    <div class="mb-3 mt-2">
      <label class="form-label fw-semibold">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="p-3 disabled-div">
      <label class="form-label text-muted d-block">Preview</label>
      <textarea class="form-control" rows="3" placeholder="Enter address here..." disabled></textarea>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);
    const requiredToggle = card.querySelector(".required-toggle");
    const titleInput = card.querySelector(".question-title");
    // Array ile senkron
    const question = surveyQuestions.find(q => q.order === questionIndex);

    // Title input
    titleInput.addEventListener("input", e => {
        if (question) question.title = e.target.value;
    });

    // Required toggle
    requiredToggle.addEventListener("change", e => {
        if (question) question.isRequired = e.target.checked;
    });
    return card;
}

function createWebsiteCard(type, questionIndex) {
    const contentHtml = `
    <div class="form-check form-switch">
      <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
      <label class="form-check-label" for="required${questionCount}">Required</label>
    </div>

    <div class="mb-3 mt-2">
      <label class="form-label fw-semibold">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="p-3 disabled-div">
      <label class="form-label text-muted d-block">Preview</label>
      <input type="url" class="form-control" placeholder="https://example.com" disabled>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);
    const requiredToggle = card.querySelector(".required-toggle");
    const titleInput = card.querySelector(".question-title");
    // Array ile senkron
    const question = surveyQuestions.find(q => q.order === questionIndex);

    // Title input
    titleInput.addEventListener("input", e => {
        if (question) question.title = e.target.value;
    });

    // Required toggle
    requiredToggle.addEventListener("change", e => {
        if (question) question.isRequired = e.target.checked;
    });
    return card;
}

function createImageUploadCard(type, questionIndex) {
    const contentHtml = `
    <div class="form-check form-switch">
      <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
      <label class="form-check-label" for="required${questionCount}">Required</label>
    </div>

    <div class="mb-3 mt-2">
      <label class="form-label fw-semibold">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="col-12 mt-3 disabled-div">
      <div class="card">
        <h5 class="card-header">Preview</h5>
        <div class="card-body">
          <form action="/upload" class="dropzone needsclick" id="dropzone-${questionCount}">
            <div class="dz-message needsclick">
              Drop files here or click to upload
              <span class="note needsclick">
                (This is just a demo dropzone. Selected files are
                <span class="fw-medium">not</span> actually uploaded.)
              </span>
            </div>
            <div class="fallback">
              <input name="file" type="file" accept="image/*"/>
            </div>
          </form>
        </div>
      </div>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);
    const requiredToggle = card.querySelector(".required-toggle");
    const question = surveyQuestions.find(q => q.order === questionIndex);
    // Required toggle
    requiredToggle.addEventListener("change", e => {
        if (question) question.isRequired = e.target.checked;
    });
    // Dropzone yüklendiyse başlat
    if (typeof Dropzone !== "undefined") {
        const dropzoneElement = card.querySelector(`#dropzone-${questionCount}`);
        if (dropzoneElement) {
            const dz = new Dropzone(dropzoneElement, {
                url: `${protocol}//${domain}${port}/services/PvSurvey/SurveyDesign/UploadImage`,
                autoProcessQueue: true,   // dosya yükleme anında başlasın
                maxFilesize: 5,           // MB
                addRemoveLinks: true,
                maxFiles: 1,
                acceptedFiles: "image/*",
                init: function () {
                    this.on("success", function (file, response) {
                        // Backend’den dönen uploadedUrl bilgisini dosyaya ekle
                        file.uploadedUrl = response.uploadedUrl;
                    });

                    this.on("removedfile", function () {
                        // Dosya kaldırıldığında uploadedUrl’yi temizle
                        delete file.uploadedUrl;
                    });
                }
            });

            // ✅ Dropzone instance’a soru index’ini iliştir (ileride kolay erişim için)
            dz.questionIndex = questionIndex;
        
        }
    }

    return card;
}

function createFileUploadCard(type, questionIndex) {
    const contentHtml = `
    <div class="form-check form-switch mb-2">
      <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
      <label class="form-check-label" for="required${questionCount}">Required</label>
    </div>

    <div class="mb-3">
      <label class="form-label fw-semibold">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="card">
      <h5 class="card-header">Preview</h5>
      <div class="card-body">
        <div class="custom-dropzone" id="dropzone-${questionCount}">
          <div class="dropzone-icon">⬆️</div>
          <div class="dropzone-text">Drop files here or click to upload</div>
          <div class="dropzone-note">(This is just a demo dropzone. Selected files are not actually uploaded.)</div>
          <input type="file" class="file-input" multiple style="display:none;">
          <div class="upload-list mt-2"></div>
        </div>
      </div>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    const dropzone = card.querySelector(`#dropzone-${questionCount}`);
    const fileInput = dropzone.querySelector(".file-input");
    const uploadList = dropzone.querySelector(".upload-list");

    // Tıklayınca input aç
    dropzone.addEventListener("click", () => fileInput.click());

    // Dosya seçildiğinde backend'e gönder
    fileInput.addEventListener("change", async (e) => {
        const files = Array.from(e.target.files);
        for (const file of files) {
            const item = document.createElement("div");
            item.textContent = `📁 ${file.name} - Uploading...`;
            uploadList.appendChild(item);

            try {
                const formData = new FormData();
                formData.append("file", file);

                const response = await fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveyDesign/UploadImage`, {
                    method: "POST",
                    body: formData
                });

                if (!response.ok) throw new Error(await response.text());
                const data = await response.json();
                item.textContent = `✅ ${file.name} uploaded successfully. URL: ${data.uploadedUrl}`;
            } catch (err) {
                item.textContent = `❌ ${file.name} upload failed: ${err.message}`;
            }
        }
    });

    return card;
}


function createVideoUploadCard(type, questionIndex) {
    const contentHtml = `
    <div class="mb-3 mt-2">
      <label class="form-label fw-semibold">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="col-12 mt-3">
      <div class="card">
        <h5 class="card-header">Preview</h5>
        <div class="card-body">
          <form action="/upload" class="dropzone needsclick" id="dropzone-${questionCount}">
            <div class="dz-message needsclick">
              Drop video here or click to upload
              <span class="note needsclick">
                (This is just a demo dropzone. Selected files are
                <span class="fw-medium">not</span> actually uploaded.)
              </span>
            </div>
            <div class="fallback">
              <input name="file" type="file" accept="video/*"/>
            </div>
          </form>

          <video
            class="w-100 mt-3"
            id="plyr-video-${questionCount}"
            poster="https://cdn.plyr.io/static/demo/View_From_A_Blue_Moon_Trailer-HD.jpg"
            playsinline
            controls
            style="display:none;"
          >
            <source src="" type="video/mp4" />
          </video>
        </div>
      </div>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    if (typeof Dropzone !== "undefined") {
        const dropzoneElement = card.querySelector(`#dropzone-${questionCount}`);
        const videoElement = card.querySelector(`#plyr-video-${questionCount}`);

        if (dropzoneElement) {
            new Dropzone(dropzoneElement, {
                parallelUploads: 1,
                maxFilesize: 50,
                addRemoveLinks: true,
                maxFiles: 1,
                acceptedFiles: "video/*",
                init: function () {
                    this.on("addedfile", function (file) {
                        const reader = new FileReader();
                        reader.onload = function (e) {
                            const source = videoElement.querySelector("source");
                            source.src = e.target.result;
                            videoElement.load(); // video elementini güncelle
                            videoElement.style.display = "block";
                        };
                        reader.readAsDataURL(file);
                    });

                    this.on("removedfile", function (file) {
                        const source = videoElement.querySelector("source");
                        source.src = "";
                        videoElement.load();
                        videoElement.style.display = "none";
                    });
                }
            });
        }
    }

    return card;
}

function createNumberCard(type, questionIndex) {
    const contentHtml = `
    <div class="d-flex gap-4 mb-3">
      <div class="form-check form-switch">
        <input class="form-check-input quizMode-toggle" type="checkbox" id="quizMode${questionCount}">
        <label class="form-check-label" for="quizMode${questionCount}">Quiz mode</label>
      </div>

      <div class="form-check form-switch">
        <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
        <label class="form-check-label" for="required${questionCount}">Required</label>
      </div>
    </div>

    <div class="mb-3 mt-2">
      <label class="form-label fw-semibold">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="mb-3">
      <label class="form-label fw-semibold">Correct Answer (number)</label>
      <input type="number" class="form-control correct-answer" placeholder="Enter correct answer" disabled>
      <small class="text-muted">Integer, float, or double values allowed</small>
    </div>

    <div class="mb-3 mt-3 disabled-div">
      <label class="form-label text-muted d-block">Preview</label>
      <input type="number" class="form-control preview-number" placeholder="Preview" disabled>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);

    // JS: Quiz mode aktif olursa correct answer input aktif olsun
    const quizToggle = card.querySelector(`#quizMode${questionCount}`);
    const requiredToggle = card.querySelector(`#required${questionCount}`);
    const titleInput = card.querySelector(".question-title");
    const correctAnswerInput = card.querySelector('.correct-answer');
    const previewInput = card.querySelector('.preview-number');

    const question = surveyQuestions.find(q => q.order === questionIndex);
   

    // Eventler
    titleInput.addEventListener("input", e => {
        if (question) question.title = e.target.value;
    });

    requiredToggle.addEventListener("change", e => {
        if (question) question.isRequired = e.target.checked;
    });

   
    if (quizToggle && correctAnswerInput) {
        quizToggle.addEventListener("change", e => {
            if (question) question.isQuizMode = e.target.checked;
            correctAnswerInput.disabled = !e.target.checked;
            if (!e.target.checked && question) question.correctAnswer = "";
        });
    }



    // Correct answer input girildiğinde preview alanına yansısın
    if (correctAnswerInput && previewInput) {
        correctAnswerInput.addEventListener("input", e => {
            previewInput.value = e.target.value;
            if (question) question.correctAnswer = e.target.value;
        });
    }

    return card;
}

function createNumericRangeCard(type, questionIndex) {
    const contentHtml = `
    <div class="form-check form-switch mb-3">
      <input class="form-check-input required-toggle" type="checkbox" id="required${questionCount}">
      <label class="form-check-label" for="required${questionCount}">Required</label>
    </div>

    <div class="mb-3 mt-2">
      <label class="form-label fw-semibold">Question Title</label>
      <input type="text" class="form-control question-title" placeholder="New Question">
    </div>

    <div class="row mb-3">
      <div class="col">
        <label class="form-label">Min Value</label>
        <input type="number" class="form-control min-value" placeholder="Min Value">
      </div>
      <div class="col">
        <label class="form-label">Max Value</label>
        <input type="number" class="form-control max-value" placeholder="Max Value">
      </div>
    </div>

    <div class="mb-3">
      <label class="form-label">Unit (optional)</label>
      <input type="text" class="form-control unit" placeholder="Unit">
    </div>

    <div class="mb-3 mt-3 disabled-div">
      <label class="form-label text-muted">Preview</label>
      <input type="number" class="form-control preview-number" placeholder="Preview" disabled>
      <div class="alert alert-success mt-2 valid-range-alert" role="alert">Valid range: -</div>
    </div>
  `;

    const card = createBaseCard(type, contentHtml);
    const requiredToggle = card.querySelector(`.required-toggle`);
    const minInput = card.querySelector('.min-value');
    const maxInput = card.querySelector('.max-value');
    const unitInput = card.querySelector('.unit');
    const previewInput = card.querySelector('.preview-number');
    const validRangeAlert = card.querySelector('.valid-range-alert');
    const titleInput = card.querySelector('.question-title');

    // Yardımcı fonksiyon: surveyQuestions ile senkronize et
    function syncQuestion() {
        const question = surveyQuestions.find(q => q.order === questionIndex);
        if (!question) return;

        question.title = titleInput.value;
        question.isRequired = requiredToggle.checked;
        //question.quizMode = quizToggle.checked;
        question.minLabel = parseFloat(minInput.value) || 0;
        question.maxLabel = parseFloat(maxInput.value) || 0;
        //question.step = parseFloat(stepInput.value) || 0;
        question.unit = unitInput.value.trim();

        //if (quizToggle.checked) {
        //    const cmin = parseFloat(correctMin.value);
        //    const cmax = parseFloat(correctMax.value);
        //    question.correctAnswer = !isNaN(cmin) && !isNaN(cmax) ? `${cmin}-${cmax}` : "";
        //} else {
        //    question.correctAnswer = "";
        //}
        
    }



    function updateValidRange() {
        const min = minInput.value || '';
        const max = maxInput.value || '';
        const unit = unitInput.value ? ` ${unitInput.value}` : '';
        if (min !== '' && max !== '') {
            validRangeAlert.textContent = `Valid range: ${min} – ${max}${unit}`;
        } else {
            validRangeAlert.textContent = 'Valid range: -';
        }
        syncQuestion();
    }

    // değişiklikleri dinle
    // Input event'leri
    [titleInput, requiredToggle, minInput, maxInput, unitInput].forEach(input => {
        input.addEventListener('input', () => {
            updateValidRange();
            syncQuestion();
        });
        input.addEventListener('change', syncQuestion);
    });

    // preview input için min/max ve unit yansıtma
    if (previewInput) {
        previewInput.addEventListener('input', function () {
            // optional: buraya min/max kontrolü ekleyebilirsin
        });
    }

    return card;
}


async function loadSurvey(templateIdOrId) {
    try {
        const response = await fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveyDesign/GetSurveyDesignBySurveyId?id=${templateIdOrId}`);
        if (!response.ok) throw new Error("API error");
        const data = await response.json();
        const rawQuestions = data?.data?.questions || [];

        
        // id alanını hariç tutarak surveyQuestions oluştur
        surveyQuestions = rawQuestions.map(({ id, ...rest }) => ({
            ...rest
        }));
        renderSurveyQuestions();
    } catch (err) {
        console.error(err);
        surveyQuestions = []; // boş template
    }
}

async function GetSurveyInformation(templateIdOrId) {
    try {
        const response = await fetch(`${protocol}//${domain}:${port}/services/PvSurvey/Survey/GetSurveyById?id=${templateIdOrId}`);
        if (!response.ok) throw new Error("API error");
        const data = await response.json();
        const surveyTitle = data?.data?.name || "Untitled Survey";
        const description = data?.data?.description || "";
        const surveyTypeId = data?.data?.surveyType?.id || "";
        const targetAudienceId = data?.data?.targetAudience?.id || 0;
        const languageId = data?.data?.language?.id || "";
        const duration = data?.data?.duration || 0;

        $('#txt-survey-name').val(surveyTitle);
        $('#txt-description').val(description);
        $('#txt-duration').val(duration);

        populateSelect('ddl-survey-type', {
            apiUrl: `${protocol}//${domain}:${port}/services/PvSurvey/SurveyType/GetSurveyTypes`,
            placeholder: 'Select survey type...',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: surveyTypeId  // Tek kayıt varsa otomatik seçer

        });

        populateSelect('ddl-target-auidence', {
            apiUrl: `${protocol}//${domain}:${port}/services/PvSurvey/Survey/GetTargetAudiences`,
            placeholder: 'Select target auidence',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: targetAudienceId  // Tek kayıt varsa otomatik seçer

        });
        
        populateSelect('ddl-language', {
            apiUrl: `${protocol}//${domain}:${port}/services/PvTenant/Language/GetLanguages`,
            placeholder: 'Select language',
            valueKey: 'id',
            textKey: 'name',
            selectedValue: languageId  // Tek kayıt varsa otomatik seçer

        });

        if (surveyBreadcrumb) {
            surveyBreadcrumb.textContent = `${surveyTitle} - manage`;
        }

       
    } catch (err) {
        console.error(err);
    }
}


function renderSurveyQuestions() {
    const container = document.querySelector("#formBuilder");
    container.innerHTML = ""; // önce temizle

    surveyQuestions.forEach((question, index) => {
        let card;
        
        
        card = addFormElement(question.type,index);
        card.dataset.order = index;
        // soru objesini card inputlarına populate et
        populateCardWithData(card, question);


        // DOM'daki question title input değiştiğinde, ilgili question objesinin title alanını array'e kaydeder

        card.querySelector(".question-title").addEventListener("input", (e) => {

            if (question) {
                question.title = e.target.value;          // array’e kaydet
            }
        });




        container.appendChild(card);
    });
}
function populateCardWithData(card, question) {
    if (!card || !question) return;

    const order = parseInt(card.dataset.order);   // card ile array’i 
    // Title
    const titleInput = card.querySelector(".question-title");
    if (titleInput) titleInput.value = question.title || "";

    
    // Required switch
    const requiredToggle = card.querySelector(".required-toggle");
    if (requiredToggle) requiredToggle.checked = !!(question.required ?? question.isRequired);

    // Number Card
    const correctAnswerInput = card.querySelector(".correct-answer");
    const previewNumber = card.querySelector(".preview-number");
    const quizToggle = card.querySelector(".quizMode-toggle");

    //Correct Answer Long-text için
    const correctAnswerLongInput = card.querySelector(".correctAnswer-toggle");
    const quizAnswer = card.querySelector(".quiz-answer");


    if (quizToggle) {
        quizToggle.checked = !!(question.quizMode ?? question.isQuizMode);
        if (correctAnswerInput) correctAnswerInput.disabled = !quizToggle.checked;

        if (quizAnswer) quizAnswer.style.display = quizToggle.checked ? "block" : "none";


    }


    if (correctAnswerInput) correctAnswerInput.value = question.correctAnswer || "";
    if (correctAnswerLongInput) correctAnswerLongInput.value = question.correctAnswer || "";
    if (previewNumber) previewNumber.value = question.correctAnswer || "";

    // Numeric Range Card
    const minInput = card.querySelector(".min-value");
    const maxInput = card.querySelector(".max-value");
    const unitInput = card.querySelector(".unit");
    const validRangeAlert = card.querySelector(".valid-range-alert");
    if (minInput) minInput.value = question.minValue ?? question.minLabel ?? "";
    if (maxInput) maxInput.value = question.maxValue ?? question.maxLabel ?? "";
    if (unitInput) unitInput.value = question.unit || "";


    if (question.type === "slider-scale") {
        const stepInput = card.querySelector(".step-value");
        if (stepInput) {

            const step = Number.isFinite(Number(question.step)) ? Number(question.step) : 1;
            stepInput.value = step;
        } 


        if (minInput) {
            const min = parseInt(question.minValue ?? question.minLabel, 0);
            minInput.value = Number.isFinite(min) ? min : "";
        }

        if (maxInput) {
            const max = parseInt(question.maxValue ?? question.maxLabel, 10);
            maxInput.value = Number.isFinite(max) ? max : "";
        }


        const slider = card.querySelector(".slider-preview");
        if (slider) {
            const step = parseInt(question.step, 1);
            const min = parseInt(question.minValue ?? question.minLabel, 0);
            const max = parseInt(question.maxValue ?? question.maxLabel, 10);


            slider.min = min;
            slider.max = max;
            slider.step = step;

            const value = Number.isFinite(question.sliderValue) ? question.sliderValue : Math.floor((min + max) / 2);
            slider.value = Math.min(Math.max(value, min), max);
        }
        const previewValue = card.querySelector(".preview-value");
        if (previewValue) previewValue.textContent = slider.value;


    }






    if (validRangeAlert) {
        const min = question.minValue ?? question.minLabel;
        const max = question.maxValue ?? question.maxLabel;
        if (min != null && max != null) {
            validRangeAlert.textContent = `Valid range: ${min} – ${max}${question.unit ? ' ' + question.unit : ''}`;
        } else {
            validRangeAlert.textContent = "Valid range: -";
        }
    }

    // Multiple Choice (Single veya Multiple) için options set etme
    if (question.type === "single-choice") {
        

        const optionsContainer = card.querySelector(".options-container");
        const previewArea = card.querySelector(".preview-area");
        const quizToggle = card.querySelector(".quizMode-toggle");

        if (optionsContainer && Array.isArray(question.options)) {
            optionsContainer.innerHTML = "";
            previewArea.innerHTML = "";

            const renderPreview = () => {
                previewArea.innerHTML = question.options
                    .map(
                        (opt, i) => `
        <div class="form-check">
          <input class="form-check-input" type="radio" name="preview-${questionCount}" id="p${i}" ${opt.isCorrect ? "checked" : ""} disabled>
          <label class="form-check-label" for="p${i}">${opt.value || `Option ${i + 1}`}</label>
        </div>`
                    )
                    .join("");
            };


            question.options.forEach((opt, i) => {
                const optRow = document.createElement("div");
                optRow.className = "d-flex align-items-center gap-2 mb-2";
                optRow.innerHTML = `
                <input type="text" class="form-control option-text" value="${opt.value}" placeholder="Option ${i + 1}">
                <div class="form-check ms-2">
                    <input type="radio" name="correct${order + 1}" class="form-check-input correct-radio" ${opt.isCorrect ? "checked" : ""} ${!quizToggle?.checked ? "disabled" : ""}>
                    <label class="form-check-label text-muted small">Correct</label>
                </div>
                <button class="btn btn-sm btn-outline-danger remove-option"><i class="bx bx-trash"></i></button>
            `;

                // ✅ Correct değiştiğinde preview güncellensin
                const correctRadio = optRow.querySelector(".correct-radio");
                correctRadio.addEventListener("change", () => {
                    question.options.forEach(o => (o.isCorrect = false));
                    question.options[i].isCorrect = true;
                    renderPreview(); // preview'ı yeniden çiz
                });

                // ✅ Option text değiştiğinde array senkronize olsun
                const textInput = optRow.querySelector(".option-text");
                textInput.addEventListener("input", (e) => {
                    question.options[i].value = e.target.value;
                    renderPreview();
                });

                // ✅ Remove butonu (createMultipleChoiceSingleCard ile aynı mantık)
                const removeBtn = optRow.querySelector(".remove-option");
                removeBtn.addEventListener("click", () => {
                    if (question.options.length === 1) {
                        alert("En az bir option olmalıdır!");
                        return;
                    }

                    // 1. array’den çıkar
                    question.options.splice(i, 1);

                    // 2. yeniden index ver (opsiyonel ama düzenli)
                    question.options.forEach((o, idx) => (o.order = idx + 1));

                    // 3. yeniden çiz
                    // burada aynı kod bloğunu tekrar çağırıyoruz, böylece tüm eventler yeniden bağlanıyor
                    populateCardWithData(card, question);
                });


                optionsContainer.appendChild(optRow);
            });

            // Preview kısmı da doldurulsun
            previewArea.innerHTML = question.options
                .map((opt, i) => `
                <div class="form-check">
                    <input class="form-check-input" type="radio" name="preview-${order + 1}" id="p${i}" ${opt.isCorrect ? "checked" : ""} disabled>
                    <label class="form-check-label" for="p${i}">${opt.value || `Option ${i + 1}`}</label>
                </div>
            `)
                .join("");
        }
    }

    // multiple-choice için benzer kod bloğu eklenebilir
    if (question.type === "multiple-choice") {
        if (!card || !question) return;

        // 🎯 Temel alanları dolduralım
        const titleInput = card.querySelector(".question-title");
        const quizToggle = card.querySelector(".quizMode-toggle");
        const requiredToggle = card.querySelector(".required-toggle");
        const optionsContainer = card.querySelector(".options-container");
        const previewArea = card.querySelector(".preview-area");

        if (titleInput) titleInput.value = question.title || "";
        if (quizToggle) quizToggle.checked = !!question.isQuizMode;
        if (requiredToggle) {
            const requiredInput = card.querySelector(`#${requiredToggle.getAttribute("for")}`);
            if (requiredInput) requiredInput.checked = !!question.isRequired;
        }

        // 🎯 Seçenekleri yerleştirme
        if (optionsContainer && Array.isArray(question.options)) {
            optionsContainer.innerHTML = "";
            previewArea.innerHTML = "";

            question.options.forEach((opt, i) => {
                const optRow = document.createElement("div");
                optRow.className = "d-flex align-items-center gap-2 mb-2";

                optRow.innerHTML = `
                <input type="text" class="form-control option-text" value="${opt.value || ""}" placeholder="Option ${i + 1}">
                <div class="form-check ms-2">
                    <input type="checkbox" class="form-check-input correct-checkbox align-middle"
                        ${opt.isCorrect ? "checked" : ""} ${!quizToggle.checked ? "disabled" : ""}>
                    <label class="form-check-label text-muted small">Correct</label>
                </div>
                <button class="btn btn-sm btn-outline-danger remove-option"><i class="bx bx-trash"></i></button>
            `;

                // Option text input değişimi
                optRow.querySelector(".option-text").addEventListener("input", (e) => {
                    question.options[i].value = e.target.value;
                    renderPreview();
                });

                // Correct checkbox değişimi
                optRow.querySelector(".correct-checkbox").addEventListener("change", (e) => {
                    question.options[i].isCorrect = e.target.checked;
                    renderPreview();
                });

                // Option silme
                optRow.querySelector(".remove-option").addEventListener("click", () => {
                    if (question.options.length === 1) return alert("En az bir option olmalıdır!");
                    question.options.splice(i, 1);
                    populateCardWithData(card, question);
                });

                optionsContainer.appendChild(optRow);
            });

            // 🎯 Preview kısmını render et
            const renderPreview = () => {
                previewArea.innerHTML = question.options
                    .map(
                        (opt, i) => `
                <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="p${i}" ${opt.isCorrect ? "checked" : ""} disabled>
                    <label class="form-check-label" for="p${i}">${opt.value || `Option ${i + 1}`}</label>
                </div>`
                    )
                    .join("");
            };

            renderPreview();
        }

        // 🎯 Quiz mode değiştiğinde correct'ler aktif/pasif hale gelsin
        if (quizToggle) {
            quizToggle.addEventListener("change", (e) => {
                const isQuiz = e.target.checked;
                question.isQuizMode = isQuiz;

                // correct checkbox'ları yeniden oluştur
                populateCardWithData(card, question);
            });
        }




    }

    if (question.type === "dropdown") {

        const quizToggle = card.querySelector(".quizMode-toggle");
        const requiredToggle = card.querySelector(".required-toggle");
        const titleInput = card.querySelector(".question-title");
        const addOptionBtn = card.querySelector(".add-option-btn");

        // Title
        if (titleInput) titleInput.value = question.title || "";

        // Required
        if (requiredToggle) requiredToggle.checked = !!question.isRequired;

        // Quiz mode
        if (quizToggle) quizToggle.checked = !!question.isQuizMode;

        // Options array
        let options = Array.isArray(question.options) ? [...question.options] : [];

        // Eğer options boşsa en az bir option ekle
        if (options.length === 0) {
            options.push({ value: "Option 1", isCorrect: false, order: 1 });
            question.options = [...options];
        }

        // Render fonksiyonu
        const optionsContainer = card.querySelector(".options-container");
        const previewArea = card.querySelector(".preview-area");

        const renderPreview = () => {
            previewArea.innerHTML = "";
            options.forEach((opt, i) => {
                const optionEl = document.createElement("option");
                optionEl.value = i;
                optionEl.textContent = opt.value || `Option ${i + 1}`;
                if (opt.isCorrect) optionEl.selected = true;
                previewArea.appendChild(optionEl);
            });
        };

        const renderOptions = () => {
            optionsContainer.innerHTML = "";

            options.forEach((opt, i) => {
                const optRow = document.createElement("div");
                optRow.className = "d-flex align-items-center gap-2 mb-2";

                optRow.innerHTML = `
                <input type="text" class="form-control option-text" value="${opt.value}" placeholder="Option ${i + 1}">
                <div class="form-check">
                    <input class="form-check-input correct-radio" type="radio" name="correct${question.order}" ${opt.isCorrect ? "checked" : ""} ${quizToggle.checked ? "" : "disabled"}>
                    <label class="form-check-label small text-muted">Correct</label>
                </div>
                <button class="btn btn-sm btn-outline-danger remove-option"><i class="bx bx-trash"></i></button>
            `;

                // Option text değişimi
                optRow.querySelector(".option-text").addEventListener("input", e => {
                    options[i].value = e.target.value;
                    question.options = [...options];
                    renderPreview();
                });

                // Correct radio change
                optRow.querySelector(".correct-radio").addEventListener("change", e => {
                    options.forEach(o => o.isCorrect = false);
                    options[i].isCorrect = true;
                    question.options = [...options];
                    renderPreview();
                });

                // Option silme
                optRow.querySelector(".remove-option").addEventListener("click", () => {
                    const wasCorrect = options[i].isCorrect;
                    options.splice(i, 1);
                    if (wasCorrect && quizToggle.checked && options.length > 0) {
                        options[0].isCorrect = true;
                    }
                    question.options = [...options];
                    renderOptions();
                });

                optionsContainer.appendChild(optRow);
            });

            renderPreview();
        };

        renderOptions();

        // Add option button
        if (addOptionBtn) {
            addOptionBtn.addEventListener("click", () => {
                options.push({ value: `Option ${options.length + 1}`, isCorrect: false, order: options.length + 1 });
                if (quizToggle.checked && !options.some(o => o.isCorrect)) {
                    options[0].isCorrect = true;
                }
                question.options = [...options];
                renderOptions();
            });
        }

        // Quiz mode toggle
        if (quizToggle) {
            quizToggle.addEventListener("change", (ev) => {
                const isQuiz = ev.target.checked;
                question.isQuizMode = isQuiz;
                if (!isQuiz) {
                    options.forEach(o => o.isCorrect = false);
                } else if (options.length > 0 && !options.some(o => o.isCorrect)) {
                    options[0].isCorrect = true;
                }
                question.options = [...options];
                renderOptions();
            });
        }


    }

    if (question.type === "rating-scale") {

        const correctRatingSelect = card.querySelector(".correct-rating");
        const isQuiz = !!question.isQuizMode;
        if (quizToggle) quizToggle.checked = isQuiz;
        // --- Correct Rating ---
        if (correctRatingSelect) {
            correctRatingSelect.disabled = !isQuiz;
            correctRatingSelect.value = question.correctAnswer ? String(question.correctAnswer) : "";
        }

        // --- Preview Güncelle ---
        const correctVal = question.correctAnswer;
        const previewButtons = card.querySelectorAll(`[name^="rating-preview-"]`);

        previewButtons.forEach(btn => {
            const btnVal = btn.textContent.trim();

            if (correctVal && btnVal === String(correctVal)) {
                btn.classList.add("active", "bg-primary", "text-white");
            } else {
                btn.classList.remove("active", "bg-primary", "text-white");
            }
        });

    }

    if (question.type === "date") {

        const quizAnswer = card.querySelector(".quiz-answer");
        const correctAnswerInput = card.querySelector(".correct-answer-input");
        const previewDateInput = card.querySelector(".preview-date-input");

        const isQuiz = !!question.isQuizMode;
        if (quizToggle) quizToggle.checked = isQuiz;
        if (quizAnswer) quizAnswer.style.display = isQuiz ? "block" : "none";

        // --- Correct Answer ---
        if (correctAnswerInput && question.correctAnswer) {
            const fpCorrect = correctAnswerInput._flatpickr;
            if (fpCorrect) {
                fpCorrect.setDate(question.correctAnswer, true);
            } else {
                correctAnswerInput.value = question.correctAnswer;
            }
        }

        if (previewDateInput && question.correctAnswer) {
            const fpPreview = previewDateInput._flatpickr;
            if (fpPreview) {
                fpPreview.setDate(question.correctAnswer, true);
            } else {
                previewDateInput.value = question.correctAnswer;
            }
        }

    }

    if (question.type === "time") {
        const quizAnswer = card.querySelector(".quiz-answer");
        const correctAnswerTime = card.querySelector(".correct-answer-time");
        const previewDateTime = card.querySelector(".preview-date-time");

        const isQuiz = !!question.isQuizMode;
        if (quizToggle) quizToggle.checked = isQuiz;
        if (quizAnswer) quizAnswer.style.display = isQuiz ? "block" : "none";

        // --- Correct Answer (Time) ---
        if (correctAnswerTime && question.correctAnswer) {
            const fpCorrect = correctAnswerTime._flatpickr;
            if (fpCorrect) {
                fpCorrect.setDate(question.correctAnswer, true);
            } else {
                correctAnswerTime.value = question.correctAnswer;
            }
        }

        // --- Preview ---
        if (previewDateTime && question.correctAnswer) {
            const fpPreview = previewDateTime._flatpickr;
            if (fpPreview) {
                fpPreview.setDate(question.correctAnswer, true);
            } else {
                previewDateTime.value = question.correctAnswer;
            }
        }


    }

    // Video Card
    //const videoElement = card.querySelector("video");
    //if (videoElement && question.previewValue) {
    //    videoElement.src = question.previewValue;
    //    videoElement.style.display = "block";
    //}

    //// File / Image Card
    //const fileInput = card.querySelector('input[type="file"]');
    //if (fileInput && question.previewValue) {
    //    // opsiyonel: file name gösterebilirsin
    //}







}


// Input değişimlerini state’e yansıt
function attachInputListeners(card, question) {
    if (!card || !question) return;

    // Question Title
    const titleInput = card.querySelector(".question-title");
    if (titleInput) {
        titleInput.addEventListener("input", e => question.title = e.target.value);
    }

    // Required
    const requiredToggle = card.querySelector(".required-toggle");
    if (requiredToggle) {
        requiredToggle.addEventListener("change", e => question.required = e.target.checked);
    }

    // Number Card
    const quizToggle = card.querySelector(".quiz-toggle");
    const correctAnswerInput = card.querySelector(".correct-answer");
    const previewNumber = card.querySelector(".preview-number");
    if (quizToggle && correctAnswerInput) {
        quizToggle.addEventListener("change", e => {
            correctAnswerInput.disabled = !quizToggle.checked;
            question.quizMode = quizToggle.checked;
        });
    }
    if (correctAnswerInput && previewNumber) {
        correctAnswerInput.addEventListener("input", e => {
            question.correctAnswer = e.target.value;
            previewNumber.value = e.target.value;
        });
    }

    // Numeric Range Card
    const minInput = card.querySelector(".min-value");
    const maxInput = card.querySelector(".max-value");
    const unitInput = card.querySelector(".unit");
    const validRangeAlert = card.querySelector(".valid-range-alert");
    function updateNumericRange() {
        question.minValue = minInput.value;
        question.maxValue = maxInput.value;
        question.unit = unitInput.value;
        if (minInput.value && maxInput.value) {
            validRangeAlert.textContent = `Valid range: ${minInput.value} – ${maxInput.value}${unitInput.value ? ' ' + unitInput.value : ''}`;
        } else {
            validRangeAlert.textContent = "Valid range: -";
        }
    }
    if (minInput) minInput.addEventListener("input", updateNumericRange);
    if (maxInput) maxInput.addEventListener("input", updateNumericRange);
    if (unitInput) unitInput.addEventListener("input", updateNumericRange);
    if (previewNumber) {
        previewNumber.addEventListener("input", e => question.previewValue = e.target.value);
    }

    // Video Card
    const videoElement = card.querySelector("video");
    const videoInput = card.querySelector('input[type="file"]');
    if (videoInput && videoElement) {
        videoInput.addEventListener("change", e => {
            const file = e.target.files[0];
            if (file) {
                const url = URL.createObjectURL(file);
                videoElement.src = url;
                videoElement.style.display = "block";
                question.previewValue = url;
            }
        });
    }

    // File / Image Card
    if (fileInput) {
        fileInput.addEventListener("change", e => {
            const file = e.target.files[0];
            if (file) {
                question.previewValue = file.name; // opsiyonel olarak sadece dosya adını saklayabiliriz
            }
        });
    }
}

function addQuestion(type) {
    // 1. Yeni soru objesini oluştur (API formatına uygun)
    const newQuestion = {
        order: surveyQuestions.length, // sıra numarası
        type: type,
        title: "",
        isRequired: false,
        isQuizMode: false,
        correctAnswer: "",
        minLabel: 0,
        maxLabel: 10,
        step: 1,
        unit: "",
        matrixRows: [],
        matrixColumns: [],
        options: []
    };

    // Seçenekli sorular için default option ekle
    if (type === "single-choice" || type === "multiple-choice") {
        newQuestion.options.push({
            value: "Option 1",
            isCorrect: false,
            order: 0
        });
    }

    // 2. Array'e ekle
    surveyQuestions.push(newQuestion);

    // 3. DOM'a kart ekle
    //const card = addFormElement(type, newQuestion.order);

    //// Opsiyonel: card ile question objesini ilişkilendirmek istersen
    //card.dataset.order = newQuestion.order;

    // 4. Render veya update gerekli ise
    renderSurveyQuestions();

}






async function saveSurvey() {

    const name = document.getElementById('txt-survey-name').value;
    const description = document.getElementById('txt-description')?.value || "";
    const surveyType = document.getElementById('ddl-survey-type').value;
    const targetAudience = document.getElementById('ddl-target-auidence').value;
    const language = document.getElementById('ddl-language').value;
    const duration = document.getElementById('txt-duration').value.trim();



    try {
        const payload = {
            surveyId: surveyId,
            name: name,
            description: description,
            surveyTypeId: surveyType,
            targetAuidienceId: targetAudience,
            languageId: language,
            duration: parseInt(duration) || 0,
            questions: surveyQuestions.map(q => ({
                order: q.order ?? 0,
                type: q.type ?? "",
                isQuizMode: q.isQuizMode ?? false,
                isRequired: q.isRequired ?? false,
                title: q.title ?? "",
                correctAnswer: q.correctAnswer ?? "",
                minLabel: q.minLabel ?? 0,
                maxLabel: q.maxLabel ?? 0,
                step: q.step ?? 0,
                unit: q.unit ?? "",
                matrixRows: q.matrixRows || [],
                matrixColumns: q.matrixColumns || [],
                options: (q.options || []).map(o => ({
                    value: o.value ?? "",
                    isCorrect: o.isCorrect ?? false,
                    order: o.order ?? 0
                }))
            })),
            createdBy: userName || "system" // opsiyonel parametre
        };
        const response = await fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveyDesign/CreateSurveyDesign`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });
        if (!response.ok) throw new Error("Save failed");
        alert("Survey saved successfully!");
    } catch (err) {
        console.error(err);
        alert("Error saving survey");
    }
}

document.getElementById("btnSaveSurvey").addEventListener("click", () => {
    saveSurvey(surveyId, userName);
});
