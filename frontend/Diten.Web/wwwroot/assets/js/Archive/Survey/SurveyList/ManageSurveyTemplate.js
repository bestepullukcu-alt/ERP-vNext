'use strict';
const userName = window.getUserName();
const surveyBreadcrumb = document.getElementById('surveyNameBreadcrumb');

// Dummy veri (gerçek API gelene kadar)
const templates = [
    {
        id: 1,
        title: "Employee Satisfaction Survey",
        description: "Measure how satisfied employees are with their work environment.",
        duration: "5-7 min",
        questionCount: 12
    },
    {
        id: 2,
        title: "Customer Feedback Survey",
        description: "Collect customer insights for product improvement.",
        duration: "3-5 min",
        questionCount: 8
    },
    {
        id: 3,
        title: "Training Evaluation Survey",
        description: "Evaluate the effectiveness of recent training sessions.",
        duration: "6-8 min",
        questionCount: 10
    }
];
let surveyId;

$(document).on('click', '#surveyListLink', function () {
    window.location.href = `/survey/survey-list?filterSurveyId=${surveyId}`;
});

// Sayfa yüklenince dummy data ile render et
document.addEventListener("DOMContentLoaded", () => {

    const pathParts = window.location.pathname.split("/");
    surveyId = pathParts[2];

    GetSurveyInformation(surveyId);

    renderTemplates(templates);
});

document.getElementById("startBlankBtn").addEventListener("click", () => {
    window.location.href = `/survey/${surveyId}/manage?templateId=blank`;
});

async function loadTemplates() {
    try {
        const response = await fetch('/api/surveys/templates'); // backend endpoint
        const data = await response.json();
        renderTemplates(data);
    } catch (err) {
        console.error("Template yüklenirken hata:", err);
    }
}

function renderTemplates(data) {
    const container = document.getElementById("templateContainer");
    container.innerHTML = ""; // Önce temizle

    data.forEach(t => {
        const card = document.createElement("div");
        card.className = "col-md-6 col-lg-6";

        card.innerHTML = `
      <div class="card h-100 shadow-sm">
        <div class="card-body">
          <h5 class="card-title">${t.title}</h5>
          <p class="card-text text-muted small mb-2">${t.description}</p>
          <p class="text-muted extra-small mb-3">
            <i class="bx bx-timer"></i> ${t.duration} - ${t.questionCount} questions
          </p>
          <a href="javascript:void(0)" 
             class="btn btn-label-primary"
             onclick="useTemplate(${t.id})">
             Use Template
          </a>
        </div>
      </div>
    `;

        container.appendChild(card);
    });
}

function useTemplate(templateId) {
    console.log("Seçilen Template ID:", templateId);
    // örnek: /surveys/{id}/manage?templateId=templateId yönlendirmesi
    window.location.href = `/surveys/123/manage?templateId=${templateId}`;
}

async function GetSurveyInformation(templateIdOrId) {
    try {
        const response = await fetch(`${window.ApiBaseUrl}/services/PvSurvey/Survey/GetSurveyById?id=${templateIdOrId}`);
        if (!response.ok) throw new Error("API error");
        const data = await response.json();
        const surveyTitle = data?.data?.name || "Untitled Survey";
        if (surveyBreadcrumb) {
            surveyBreadcrumb.textContent = `${surveyTitle} - manage`;
        }


    } catch (err) {
        console.error(err);
    }
}


