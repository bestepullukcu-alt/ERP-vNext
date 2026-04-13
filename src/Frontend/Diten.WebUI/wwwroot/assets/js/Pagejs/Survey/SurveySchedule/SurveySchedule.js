'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
const userName = window.getUserName();
const dtStartDate = document.querySelector('#dtStartDate');
dtStartDate.flatpickr({
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    minDate: "today" // bugünden küçük tarih seçilemez
});
const dtEndDate = document.querySelector('#dtEndDate');
dtEndDate.flatpickr({
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    minDate: "today"
});
const updatedtStartDate = document.querySelector('#updatedtStartDate');
updatedtStartDate.flatpickr({
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    minDate: "today" // bugünden küçük tarih seçilemez
});
const updatedtEndDate = document.querySelector('#updatedtEndDate');
updatedtEndDate.flatpickr({
    altInput: true,
    altFormat: 'd.m.Y',
    dateFormat: 'Y-m-d',
    static: true,
    minDate: "today"
});
let currentScheduleId = null; // global değişken
let schedulesData = []; // Tüm schedul'lar burada tutulacak


document.addEventListener('DOMContentLoaded', function () {
    const btnSchedule = document.getElementById('btnSchedule');
    const offcanvasEl = document.getElementById('offcanvasCreateSchedule');

    loadSchedules();


    if (btnSchedule && offcanvasEl) {
        btnSchedule.addEventListener('click', function () {
            const offcanvas = new bootstrap.Offcanvas(offcanvasEl);
            const createScheduleForm = document.getElementById('createScheduleForm');
            if (!createScheduleForm) return;
            createScheduleForm.reset();

            // Select2 kullanıyorsan refresh
            ['ddl-survey'].forEach(id => {
                const select = document.getElementById(id);
                if (typeof $ !== 'undefined' && $(select).hasClass('select2')) {
                    $(select).val(null).trigger('change.select2');
                }
            });

            offcanvas.show();
        });
    }
    populateSelect('add-target-auidence', {
        apiUrl: `${protocol}//${domain}:${port}/services/PvSurvey/Survey/GetTargetAudiences`,
        placeholder: 'Select target auidence',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

    });
    populateSelect('ddl-survey', {
        apiUrl: `${protocol}//${domain}:${port}/services/PvSurvey/Survey/GetSurveys`,
        placeholder: 'Select survey',
        valueKey: 'id',
        textKey: 'name',
        autoSelectIfSingle: true  // Tek kayıt varsa otomatik seçer

    });

    initializeScheduleFormValidation();
    initializeUpdateScheduleFormValidation();


});

// start date değiştiğinde end date’in minDate’ini güncelle
dtStartDate.addEventListener('change', function (e) {
    dtEndDate._flatpickr.set('minDate', e.target.value);
});
function initializeScheduleFormValidation() {
    const createScheduleForm = document.getElementById('createScheduleForm');
    if (!createScheduleForm) return;

    const fv = FormValidation.formValidation(createScheduleForm, {
        fields: {
            surveyName: {
                validators: {
                    notEmpty: { message: 'Please select a survey' }
                }
            },
            targetAuidence: {
                validators: {
                    notEmpty: { message: 'Please select a target audience' }
                }
            },
            dateStart: {
                validators: {
                    notEmpty: { message: 'Start date is required' },
                    callback: {
                        message: 'Start date cannot be before today',
                        callback: function (input) {
                            if (!input.value) return true;
                            const selectedDate = new Date(input.value);
                            const today = new Date();
                            today.setHours(0, 0, 0, 0);
                            return selectedDate >= today;
                        }
                    }
                }
            },
            dateEnd: {
                validators: {
                    callback: {
                        message: 'End date must be after or equal to start date',
                        callback: function (input) {
                            const start = document.getElementById('dtStartDate').value;
                            if (!input.value || !start) return true;
                            return new Date(input.value) >= new Date(start);
                        }
                    }
                }
            },
            targetResponse: {
                validators: {
                    integer: { message: 'Target response must be a number' },
                    greaterThan: {
                        min: 1,
                        message: 'Target response must be greater than 0'
                    }
                }
            },
            remainderSchedule: {
                validators: {
                    regexp: {
                        regexp: /^(\d+(,\d+)*)?$/,
                        message: 'Only comma-separated positive integers allowed (e.g. 7,3,1)'
                    }
                }
            }
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: '',
                rowSelector: '.form-control-validation'
            }),
            submitButton: new FormValidation.plugins.SubmitButton(),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });

    handleScheduleSubmit(fv);
}


function handleScheduleSubmit(fv) {
    fv.on('core.form.valid', function () {
        const surveyId = document.getElementById('ddl-survey').value;
        const targetAudienceId = document.getElementById('add-target-auidence').value;
       const targetResponse = document.getElementById('add-target-response').value;
        const remainderSchedule = document.getElementById('add-remainder-schedule').value;
        // virgülle ayrılmış stringi array hâline çevir
        let reminderDays = [];
        if (remainderSchedule) {
            reminderDays = remainderSchedule
                .split(',')
                .map(d => parseInt(d.trim()))
                .filter(d => !isNaN(d) && d > 0); // sadece pozitif integer
        }

        const fltStart = document.querySelector('#dtStartDate')._flatpickr;
        const startDateVal = fltStart.selectedDates[0];
        let isoDateStart;
        if (startDateVal) {
            isoDateStart = new Date(startDateVal.getTime() - startDateVal.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }
        const fltEnd = document.querySelector('#dtEndDate')._flatpickr;
        const endDateVal = fltEnd.selectedDates[0];
        let isoDateEnd;
        if (endDateVal) {
            isoDateEnd = new Date(endDateVal.getTime() - endDateVal.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }


        // form objesi oluştur
        const payload = {
            surveyId,
            targetAudienceId,
            startDate: isoDateStart,
            endDate: isoDateEnd || null,
            targetResponseCount: targetResponse ? parseInt(targetResponse) : null,
            reminderDays,
            createdBy: userName
        };


        fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveySchedule/CreateSchedule`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(payload)
        })
            .then(res => res.json())
            .then(data => {
                showToast('The schedule has been created successfully.', 'success');
                fv.resetForm(true);
                createScheduleForm.reset();
                const offcanvasEl = document.getElementById('offcanvasCreateSchedule');
                const offcanvas = bootstrap.Offcanvas.getInstance(offcanvasEl);
                if (offcanvas) offcanvas.hide();

                loadSchedules();
            })
            .catch(err => {
                console.error(err);
                showToast('Error while creating schedule.', 'danger');
            });
    });
}


function initializeUpdateScheduleFormValidation() {
    const updateScheduleForm = document.getElementById('updateScheduleForm');
    if (!updateScheduleForm) return;

    const fv = FormValidation.formValidation(updateScheduleForm, {
        fields: {
            surveyName: {
                validators: {
                    notEmpty: { message: 'Please select a survey' }
                }
            },
            targetAuidence: {
                validators: {
                    notEmpty: { message: 'Please select a target audience' }
                }
            },
            dateStart: {
                validators: {
                    notEmpty: { message: 'Start date is required' },
                    callback: {
                        message: 'Start date cannot be before today',
                        callback: function (input) {
                            if (!input.value) return true;
                            const selectedDate = new Date(input.value);
                            const today = new Date();
                            today.setHours(0, 0, 0, 0);
                            return selectedDate >= today;
                        }
                    }
                }
            },
            dateEnd: {
                validators: {
                    callback: {
                        message: 'End date must be after or equal to start date',
                        callback: function (input) {
                            const start = document.getElementById('updatedtStartDate').value;
                            if (!input.value || !start) return true;
                            return new Date(input.value) >= new Date(start);
                        }
                    }
                }
            },
            targetResponse: {
                validators: {
                    integer: { message: 'Target response must be a number' },
                    greaterThan: {
                        min: 1,
                        message: 'Target response must be greater than 0'
                    }
                }
            },
            remainderSchedule: {
                validators: {
                    regexp: {
                        regexp: /^(\d+(,\d+)*)?$/,
                        message: 'Only comma-separated positive integers allowed (e.g. 7,3,1)'
                    }
                }
            }
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: '',
                rowSelector: '.form-control-validation'
            }),
            submitButton: new FormValidation.plugins.SubmitButton(),
            autoFocus: new FormValidation.plugins.AutoFocus()
        }
    });

    handleUpdateScheduleSubmit(fv);
}

function handleUpdateScheduleSubmit(fv) {
    fv.on('core.form.valid', function () {
        const surveyId = document.getElementById('update-ddl-survey').value;
        const targetAudienceId = document.getElementById('update-target-auidence').value;
        const targetResponse = document.getElementById('update-target-response').value;
        const remainderSchedule = document.getElementById('update-remainder-schedule').value;
        // virgülle ayrılmış stringi array hâline çevir
        let reminderDays = [];
        if (remainderSchedule) {
            reminderDays = remainderSchedule
                .split(',')
                .map(d => parseInt(d.trim()))
                .filter(d => !isNaN(d) && d > 0); // sadece pozitif integer
        }

        const fltStart = document.querySelector('#updatedtStartDate')._flatpickr;
        const startDateVal = fltStart.selectedDates[0];
        let isoDateStart;
        if (startDateVal) {
            isoDateStart = new Date(startDateVal.getTime() - startDateVal.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }
        const fltEnd = document.querySelector('#updatedtEndDate')._flatpickr;
        const endDateVal = fltEnd.selectedDates[0];
        let isoDateEnd;
        if (endDateVal) {
            isoDateEnd = new Date(endDateVal.getTime() - endDateVal.getTimezoneOffset() * 60000); // "2025-05-11T10:45:56.435Z"
        }


        // form objesi oluştur
        const payload = {
            currentScheduleId,
            surveyId,
            targetAudienceId,
            startDate: isoDateStart,
            endDate: isoDateEnd || null,
            targetResponseCount: targetResponse ? parseInt(targetResponse) : null,
            reminderDays,
            modifiedBy: userName
        };


        fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveySchedule/UpdateSchedule`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(payload)
        })
            .then(res => res.json())
            .then(data => {
                showToast('The schedule has been updated successfully.', 'success');
                fv.resetForm(true);
                updateScheduleForm.reset();
                const offcanvasEl = document.getElementById('offcanvasUpdateSchedule');
                const offcanvas = bootstrap.Offcanvas.getInstance(offcanvasEl);
                if (offcanvas) offcanvas.hide();

                loadSchedules();
            })
            .catch(err => {
                console.error(err);
                showToast('Error while creating schedule.', 'danger');
            });
    });
}





async function loadSchedules() {
    try {
        const response = await fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveySchedule/GetSchedules`);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        schedulesData = data?.data || []; // burada kaydediyoruz
        renderSchedules(data?.data);
    } catch (err) {
        console.error("Schedule yüklenirken hata:", err);
        // İsteğe bağlı: kullanıcıya toast veya alert gösterebilirsin
    }
}
function renderSchedules(data) {
    const container = document.getElementById("templateContainer");
    container.innerHTML = ""; // Önce temizle

    data.forEach(t => {
        const card = document.createElement("div");
        card.className = "col-md-12 col-lg-12 mb-3";

        // Survey status badge
        let statusBadge = '';
        switch (t.scheduleStatus) {
            case 0:
                statusBadge = '<span class="badge bg-label-primary">schedule</span>';
                break;
            case 1:
                statusBadge = '<span class="badge bg-label-success">active</span>';
                break;
            case 2:
                statusBadge = '<span class="badge bg-label-warning">paused</span>';
                break;
        }

        // Pause / Active butonu dinamik
        let pauseButton = '';
        if (t.scheduleStatus === 0 || t.scheduleStatus === 1) {
            pauseButton = `
                <a href="javascript:void(0)" 
                   class="btn btn-sm btn-label-secondary"
                   onclick="pauseTemplate('${t.id}')"><i class="bx bx-pause"></i> Pause</a>
            `;
        } else if (t.scheduleStatus === 2) {
            pauseButton = `
                <a href="javascript:void(0)" 
                   class="btn btn-sm btn-label-warning"
                   onclick="activateTemplate('${t.id}')"><i class="bx bx-play"></i> Start</a>
            `;
        }

        // Tarihleri dd.MM.yyyy formatına çevir
        const formatDate = (isoDate) => {
            if (!isoDate) return '-';
            const d = new Date(isoDate);
            const day = String(d.getDate()).padStart(2, '0');
            const month = String(d.getMonth() + 1).padStart(2, '0');
            const year = d.getFullYear();
            return `${day}.${month}.${year}`;
        };

        const startDate = formatDate(t.startDate);
        const endDate = formatDate(t.endDate);

        card.innerHTML = `
            <div class="card h-100 shadow-sm">
                <div class="card-body">
                    <!-- Üst satır: Survey Name + Badge -->
                    <div class="d-flex justify-content-between align-items-center mb-2">
                        <h5 class="card-title mb-0">${t.surveyName}</h5>
                        ${statusBadge}
                    </div>

                    <!-- Alt satır: Start-End solda, Target Audience ortada -->
                    <div class="d-flex align-items-center mb-3 position-relative">
                        <!-- Start-End solda -->
                        <div class="flex-shrink-0 text-muted">
                          <i class="bx bx-calendar"></i>   ${startDate} - ${endDate}
                        </div>

                        <!-- Target Audience ortada -->
                        <div class="position-absolute start-50 translate-middle-x text-muted">
                         <i class="bx bx-user"></i>    ${t.targetAudience ? t.targetAudience.name : '-'}
                        </div>
                    </div>

                    <div class="d-flex align-items-center mb-3 position-relative">
                        <!-- Start-End solda -->
                        <div class="flex-shrink-0 text-muted">
                        <i class="bx bx-envelope"></i>   Remainders:${t.reminderDays && t.reminderDays.length ? t.reminderDays.join(',') : '-'}
                        </div>

                        <!-- Target Audience ortada -->
                        <div class="position-absolute start-50 translate-middle-x text-muted">
                          <i class="bx bx-alarm"></i> ${t.responsesInfo} responses
                        </div>
                    </div>


                    <!-- Action butonları -->
                    <div class="d-flex gap-1">
                        <a href="javascript:void(0)" 
                           class="btn btn-sm btn-label-secondary"
                           onclick="editSchedule('${t.id}')"><i class="bx bx-pencil"></i> Edit</a>
                        ${pauseButton}
                        <a href="javascript:void(0)"
                           class="btn btn-sm btn-label-secondary"
                           onclick="settingsTemplate('${t.id}')"><i class="bx bx-cog"></i> Settings</a>
                    </div>
                </div>
            </div>
        `;

        container.appendChild(card);
    });
}


async function editSchedule(scheduleId) {
    currentScheduleId = scheduleId;
    const offcanvasEl = document.getElementById('offcanvasUpdateSchedule');
    const offcanvas = new bootstrap.Offcanvas(offcanvasEl);
    const updateScheduleForm = document.getElementById('updateScheduleForm');
    if (!updateScheduleForm) return;
    updateScheduleForm.reset();

    /*try {*/
        // 1️⃣ Tüm veriler zaten loadSchedules'dan geldi
        const data = schedulesData.find(s => s.id === scheduleId);

        if (!data) {
            console.warn(`Schedule (${scheduleId}) bulunamadı.`);
            offcanvas.show();
            return;
        }

    populateSelect('update-target-auidence', {
            apiUrl: `${protocol}//${domain}:${port}/services/PvSurvey/Survey/GetTargetAudiences`,
            placeholder: 'Select target auidence',
            valueKey: 'id',
            textKey: 'name',
          selectedValue: data.targetAudience.id || 0  // Tek kayıt varsa otomatik seçer

        });
    populateSelect('update-ddl-survey', {
            apiUrl: `${protocol}//${domain}:${port}/services/PvSurvey/Survey/GetSurveys`,
            placeholder: 'Select survey',
            valueKey: 'id',
            textKey: 'name',
        selectedValue: data.surveyId || ""  // Tek kayıt varsa otomatik seçer

        });

    if (window.flatpickr) {
        const startPicker = document.getElementById('updatedtStartDate')?._flatpickr;
        const endPicker = document.getElementById('updatedtEndDate')?._flatpickr;

        if (startPicker && data.startDate) {
            startPicker.setDate(data.startDate, true);
        }
        if (endPicker && data.endDate) {
            endPicker.setDate(data.endDate, true);
        }
    } else {
        // Flatpickr yüklü değilse fallback
        document.getElementById('updatedtStartDate').value = formatDate(data.startDate);
        document.getElementById('updatedtEndDate').value = formatDate(data.endDate);
    }

    document.getElementById('update-target-response').value = data.targetResponseCount ?? "";
    document.getElementById('update-remainder-schedule').value = Array.isArray(data.reminderDays)
        ? data.reminderDays.join(',')
        : (data.reminderDays ?? "");

        //// 3️⃣ Eğer Select2 varsa güncelle
        //if (typeof $ !== 'undefined') {
        //    ['ddl-survey', 'add-target-auidence'].forEach(id => {
        //        const select = $(`#${id}`);
        //        if (select.hasClass('select2')) select.trigger('change.select2');
        //    });
        //}

        // 4️⃣ Offcanvas aç
        offcanvas.show();

    //} catch (err) {
    //    console.error("Schedule verisi doldurulurken hata:", err);
    //    offcanvas.show();
    //}
}
// Yardımcı tarih format fonksiyonu
function formatDate(dateStr) {
    if (!dateStr) return "";
    const date = new Date(dateStr);
    if (isNaN(date)) return "";
    return date.toISOString().split("T")[0]; // örn: 2025-11-03
}



async function pauseTemplate(scheduleId) {
    currentScheduleId = scheduleId;
    const command = {
        id: currentScheduleId,
        status:false,
        // Eğer mevcut settingsId varsa buraya doldurabilirsin
        modifiedBy: userName || 'system' // Kullanıcı adı, variable olarak tanımlı olmalı
    };
    fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveySchedule/UpdateScheduleStatus`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(command)
    }).then(res => res.json()).then(data => {
        loadSchedules();
        showToast('The schedule has been paused.', 'error');
        console.log('Settings saved:', data);
    }).catch(err => console.error(err));

    
}
async function activateTemplate(scheduleId) {
    currentScheduleId = scheduleId;
    const command = {
        id: currentScheduleId,
        status: true,
        // Eğer mevcut settingsId varsa buraya doldurabilirsin
        modifiedBy: userName || 'system' // Kullanıcı adı, variable olarak tanımlı olmalı
    };
    fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveySchedule/UpdateScheduleStatus`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(command)
    }).then(res => res.json()).then(data => {
        loadSchedules();
        showToast('The schedule has been activated.', 'success');
        console.log('Settings saved:', data);
    }).catch(err => console.error(err));

    


}


async function settingsTemplate(scheduleId) {
    currentScheduleId = scheduleId;

    const modal = new bootstrap.Modal(document.getElementById('settingsModal'));
    modal.show();

    try {
        const response = await fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveySchedule/GetSettingsByScheduleId?id=${scheduleId}`);
        const result = await response.json();

        // API'den data var mı kontrol et
        const data = result?.data;

        // Toggle buttonları seç
        const toggleButtons = document.querySelectorAll('.toggle-setting');

        toggleButtons.forEach(btn => {
            const key = btn.dataset.setting;

            let value;
            if (data) {
                value = data[key]; // API'den gelen değer
            } else {
                // Default değerler
                switch (key) {
                    case 'emailReminders':
                    case 'showProgressBar':
                        value = true;
                        break;
                    default:
                        value = false;
                        break;
                }
            }

            // Buton görünümünü güncelle
            if (value) {
                btn.textContent = 'Enabled';
                btn.classList.remove('btn-label-secondary');
                btn.classList.add('btn-label-primary');
            } else {
                btn.textContent = 'Disabled';
                btn.classList.remove('btn-label-primary');
                btn.classList.add('btn-label-secondary');
            }
        });

    } catch (err) {
        console.error('Settings yüklenirken hata:', err);
        // Hata durumunda default değerler zaten uygulanır
    }
}

// Toggle button
document.querySelectorAll('.toggle-setting').forEach(btn => {
    btn.addEventListener('click', () => {
        if (btn.textContent === 'Enabled') {
            btn.textContent = 'Disabled';
            btn.classList.remove('btn-label-primary');
            btn.classList.add('btn-label-secondary');
        } else {
            btn.textContent = 'Enabled';
            btn.classList.remove('btn-label-secondary');
            btn.classList.add('btn-label-primary');
        }
    });
});
// Save settings
document.getElementById('saveSettingsBtn').addEventListener('click', () => {
    // command objesi oluştur
    const command = {
        settingsId: currentScheduleId, // Eğer mevcut settingsId varsa buraya doldurabilirsin
        createdBy: userName || 'system' // Kullanıcı adı, variable olarak tanımlı olmalı
    };

    // Toggle button değerlerini oku
    document.querySelectorAll('.toggle-setting').forEach(btn => {
        const key = btn.dataset.setting;
        command[key] = btn.textContent === 'Enabled';
    });

    console.log('Command to send:', command);

    // Burada backend API çağrısı ile gönder
    fetch(`${protocol}//${domain}:${port}/services/PvSurvey/SurveySchedule/CreateSettingsByScheduleId`, {
         method: 'POST',
         headers: { 'Content-Type': 'application/json' },
         body: JSON.stringify(command)
     }).then(res => res.json()).then(data => {
         console.log('Settings saved:', data);
     }).catch(err => console.error(err));

    // Modalı kapat
    const modalEl = document.getElementById('settingsModal');
    const modal = bootstrap.Modal.getInstance(modalEl);
    modal.hide();
});
function showToast(message, type = 'success') {
    const toastEl = document.getElementById('appToast');
    if (!toastEl) return; // toast element yoksa çık

    const toastBody = toastEl.querySelector('.toast-body');
    const toastHeader = toastEl.querySelector('#appToastHeader');

    if (toastBody) toastBody.textContent = message;

    if (toastHeader) {
        // Type’a göre header text veya class değiştirilebilir
        toastHeader.textContent = type.charAt(0).toUpperCase() + type.slice(1); // Baş harf büyük
        toastHeader.className = ''; // Önce class temizle
        toastHeader.classList.add('toast-header', `bg-${type}`, 'text-white'); // Örnek: bg-success, bg-warning
    }


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
