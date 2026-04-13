'use strict';

/**
 * FINISH WORKSPACE MODULE
 * Manages the full-page task completion flow.
 */
const FinishWorkspace = (function () {
    // --- State ---
    let state = {
        taskId: null,
        taskData: null,
        achievements: [],
        challenges: [],
        learnings: [],
        nextSteps: [], // [{ name, type, data }]
        outcomes: {
            files: [],
            text: { title: '', content: '' },
            links: []
        },
        validation: {
            canComplete: false,
            hardBlocked: false,
            reasons: []
        }
    };

    // --- Variables ---
    const labelColor = (typeof config !== 'undefined') ? config.colors.textMuted : '#a8aaae';
    let chartInstances = {
        spent: null,
        estimate: null,
        sessions: null,
        efficiency: null
    };

    // --- Initialization ---
    function initFinishTaskPage(taskId) {
        state.taskId = taskId;
        init();
    }

    async function init() {
        const urlParams = new URLSearchParams(window.location.search);
        state.taskId = urlParams.get('taskId');

        if (!state.taskId) {
            console.error("Task ID missing in URL");
            return;
        }

        setupEventListeners();
        setupFlatpickr();
        await loadTaskData();
    }

    function setupEventListeners() {
        // Achievements, Challenges, Learnings
        $('#btnAddAchievement').on('click', () => {
            const val = $('#ftAchievementInput').val().trim();
            if (val) { state.achievements.push(val); renderAchievements(); $('#ftAchievementInput').val(''); validate(); }
        });
        $('#btnAddChallenge').on('click', () => {
            const val = $('#ftChallengeInput').val().trim();
            if (val) { state.challenges.push(val); renderChallenges(); $('#ftChallengeInput').val(''); validate(); }
        });
        $('#btnAddLearning').on('click', () => {
            const val = $('#ftLearningInput').val().trim();
            if (val) { state.learnings.push(val); renderLearnings(); $('#ftLearningInput').val(''); validate(); }
        });

        // Next Steps
        $('#btnAddNewStepTask').on('click', () => {
            const name = prompt("Enter the name of the follow-up task:");
            if (name) {
                state.nextSteps.push({
                    name: name,
                    type: 'task',
                    data: { derivedFrom: state.taskId, parentName: state.taskData.name }
                });
                renderNextSteps();
                validate();
            }
        });

        // Meeting Toggle
        $('#ftScheduleMeetingSwitch').on('change', function () {
            if (this.checked) {
                $('#ftMeetingPlanArea').removeClass('d-none');
                // Pre-fill agenda
                const taskName = state.taskData.name;
                const notes = $('#ftCompletionNotes').val();
                $('#ftMeetingAgenda').val(`Discussing: ${taskName}\n\nOutcome Summary: ${notes}`);
            } else {
                $('#ftMeetingPlanArea').addClass('d-none');
            }
            validate();
        });

        // Outcome Link
        $('#btnAddingLink').on('click', () => {
            const link = $('#ftLinkInput').val().trim();
            if (link) {
                state.outcomes.links.push(link);
                renderLinks();
                $('#ftLinkInput').val('');
                validate();
            }
        });

        // File Selection
        $('#ftOutcomeDropzone').on('click', () => $('#ftOutcomeFileInput').trigger('click'));
        $('#ftOutcomeFileInput').on('change', function (e) {
            const files = Array.from(e.target.files);
            files.forEach(f => {
                state.outcomes.files.push(f);
                addFileToUI(f);
            });
            validate();
        });

        // Notes Change
        $('#ftCompletionNotes').on('input', validate);
        $('#ftOutcomeTitle, #ftOutcomeContent').on('input', () => {
            state.outcomes.text.title = $('#ftOutcomeTitle').val();
            state.outcomes.text.content = $('#ftOutcomeContent').val();
            validate();
        });

        // Completion Actions
        $('#btnFinishWorkspace').on('click', () => handleCompleteAction(false));
        $('#btnForceComplete').on('click', () => handleCompleteAction(true));
        $('#btnDiscardFinish').on('click', () => history.back());

        // Accordion / Collapse Lazy Init (for any charts that might be moved into accordions)
        document.querySelectorAll('.accordion-collapse').forEach(acc => {
            acc.addEventListener('shown.bs.collapse', () => {
                if (state.taskData) initSummaryCharts(state.taskData);
            });
        });
    }

    function setupFlatpickr() {
        if (typeof flatpickr !== 'undefined') {
            flatpickr('#ftMeetingDate', { dateFormat: 'd.m.Y', defaultDate: 'today' });
            flatpickr('#ftMeetingTime', { enableTime: true, noCalendar: true, dateFormat: 'H:i', time_24hr: true, defaultDate: '10:15' });
        }
    }

    // --- Data Loading (Mock API) ---
    async function loadTaskData() {
        try {
            // Simulated fetch
            await new Promise(r => setTimeout(r, 800));

            // Mock Task Object
            state.taskData = {
                id: state.taskId,
                name: "Implement Workflow Engine v2",
                typeName: "Internal Project",
                estimatedHour: 12,
                totalLogged: "14h 20m",
                timerSessions: [
                    { endPretty: "29.01.2024 18:30", duration: "4h 10m" },
                    { endPretty: "30.01.2024 12:15", duration: "2h 45m" },
                    { endPretty: "30.01.2024 16:50", duration: "7h 25m" }
                ],
                // Security / Guardrail Mocks
                hasOpenSubtasks: false,
                checklistTotal: 5,
                checklistCompleted: 5
            };

            updateUIFromTask();
            validate();
        } catch (err) {
            console.error("Failed to load task:", err);
            showToast("Error loading work package data", "error");
        }
    }

    function updateUIFromTask() {
        const d = state.taskData;

        $('#ftTaskName').text(d.name);
        $('#ftTaskNameHeader').text(d.name);
        $('#ftTaskBadgeContainer').html(`<span class="badge bg-label-primary shadow-none me-2">${d.typeName}</span>`);

        renderSummaryCards(d);
        initSummaryCharts(d);

        renderTimerHistory(d.timerSessions);
    }

    // --- Dashboard Summary Logic ---
    function renderSummaryCards(d) {
        const efficiency = calculateEfficiency(d.estimatedHour, d.totalLogged);
        const actualMins = parseDurationToMinutes(d.totalLogged);
        const estMins = (d.estimatedHour || 0) * 60;
        const variance = actualMins - estMins;

        // Card 1: Total Spent
        $('#ftTotalSpentVal').text(d.totalLogged);

        // Card 2: Estimated vs Actual
        $('#ftEstVal').text(d.estimatedHour + 'h');
        $('#ftActVal').text(d.totalLogged);

        let varianceHTML = '';
        if (variance > 0) {
            varianceHTML = `<span class="badge bg-label-danger py-1 px-2"><i class="bx bx-trending-up me-1"></i> +${Math.round(variance)} min overrun</span>`;
        } else {
            varianceHTML = `<span class="badge bg-label-success py-1 px-2"><i class="bx bx-trending-down me-1"></i> ${Math.abs(Math.round(variance))} min under</span>`;
        }
        $('#ftVarianceLabel').html(varianceHTML);

        // Card 3: Sessions
        $('#ftSessionsVal').text(d.timerSessions.length);
        const avg = d.timerSessions.length ? Math.round(actualMins / d.timerSessions.length) : 0;
        $('#ftAvgSessionTime').text(`${avg}m/session`);

        // Card 4: Efficiency
        $('#ftEfficiencyVal').text(efficiency.percent);
        $('#ftMultiplierBadge').text(`${efficiency.multiplier}x Multiplier`)
            .removeClass('bg-label-success bg-label-warning bg-label-danger')
            .addClass(efficiency.bgLabelClass);
    }

    function initSummaryCharts(d) {
        // Wrap in setTimeout to ensure DOM is fully ready
        setTimeout(() => {
            initTimeSpentChart(d);
            initEstimateChart(d);
            initSessionsChart(d);
            initEfficiencyChart(d);
        }, 0);
    }

    // --- Chart Initialization Functions ---

    function initTimeSpentChart(d) {
        requestAnimationFrame(() => {
            const el = document.querySelector("#chartTotalSpent");
            if (!el) return;

            if (chartInstances.spent) chartInstances.spent.destroy();
            chartInstances.spent = new ApexCharts(el, buildTimeSpentChartConfig(d));
            chartInstances.spent.render();
        });
    }

    function initEstimateChart(d) {
        requestAnimationFrame(() => {
            const el = document.querySelector("#chartEstimateActual");
            if (!el) return;

            if (chartInstances.estimate) chartInstances.estimate.destroy();
            chartInstances.estimate = new ApexCharts(el, buildEstimateChartConfig(d));
            chartInstances.estimate.render();
        });
    }

    function initSessionsChart(d) {
        requestAnimationFrame(() => {
            const el = document.querySelector('#sessionsBarChart');
            if (!el) return;

            if (chartInstances.sessions) chartInstances.sessions.destroy();
            chartInstances.sessions = new ApexCharts(el, buildSessionsChartConfig(d));
            chartInstances.sessions.render();
        });
    }

    function initEfficiencyChart(d) {
        requestAnimationFrame(() => {
            const el = document.querySelector("#chartEfficiency");
            if (!el) return;

            if (chartInstances.efficiency) chartInstances.efficiency.destroy();
            chartInstances.efficiency = new ApexCharts(el, buildEfficiencyChartConfig(d));
            chartInstances.efficiency.render();
        });
    }

    // --- Chart Config Builders ---

    function buildTimeSpentChartConfig(d) {
        return {
            chart: { type: 'area', height: 80, sparkline: { enabled: true }, animations: { enabled: true } },
            stroke: { curve: 'smooth', width: 2 },
            fill: { opacity: 0.3 },
            series: [{ name: 'Mins', data: [30, 45, 35, 50, 40, 60, 55] }],
            colors: ['#696cff'],
            tooltip: { fixed: { enabled: false }, x: { show: false }, y: { title: { formatter: (s) => '' } }, marker: { show: false } }
        };
    }

    function buildEstimateChartConfig(d) {
        const estStatus = ((d.estimatedHour * 60) / parseDurationToMinutes(d.totalLogged)) * 100;
        return {
            chart: { type: 'bar', height: 60, sparkline: { enabled: true } },
            plotOptions: { bar: { horizontal: true, barHeight: '40%', borderRadius: 4, colors: { backgroundBarColors: ['#f2f2f2'] } } },
            series: [{ name: 'Usage', data: [Math.min(estStatus, 100)] }],
            colors: [estStatus < 100 ? '#ff3e1d' : '#71dd37']
        };
    }

    function buildSessionsChartConfig(taskMetrics) {
        return {
            chart: {
                height: 120,
                width: 200,
                parentHeightOffset: 0,
                type: 'bar',
                toolbar: { show: false },
                sparkline: { enabled: false } // Disabled to allow xaxis labels as requested
            },
            plotOptions: {
                bar: {
                    columnWidth: '60%',
                    barHeight: '75%',
                    borderRadius: 7,
                    distributed: true,
                    startingShape: 'rounded',
                    endingShape: 'rounded'
                }
            },
            colors: [
                'rgba(105, 108, 255, 0.25)', // Pastel
                'rgba(105, 108, 255, 0.25)',
                'rgba(105, 108, 255, 0.8)',  // Highlighted day (Wednesday-ish)
                'rgba(105, 108, 255, 0.25)',
                'rgba(105, 108, 255, 0.25)',
                'rgba(105, 108, 255, 0.25)',
                'rgba(105, 108, 255, 0.25)'
            ],
            grid: {
                show: false,
                padding: {
                    top: -20,
                    bottom: -10,
                    left: 0,
                    right: 0
                }
            },
            dataLabels: { enabled: false },
            legend: { show: false },
            series: [{
                name: 'Sessions',
                data: taskMetrics.timerSessions && taskMetrics.timerSessions.length > 0
                    ? taskMetrics.timerSessions.map(s => parseDurationToMinutes(s.duration))
                    : [40, 95, 60, 45, 90, 50, 75]
            }],
            xaxis: {
                categories: ['M', 'T', 'W', 'T', 'F', 'S', 'S'],
                axisBorder: { show: false },
                axisTicks: { show: false },
                labels: {
                    style: {
                        fontSize: '12px',
                        colors: labelColor,
                        fontFamily: 'Public Sans'
                    }
                }
            },
            yaxis: { labels: { show: false } },
            tooltip: {
                enabled: true,
                shared: false,
                intersect: true,
                x: { show: true }
            },
            responsive: [
                {
                    breakpoint: 1441,
                    options: { plotOptions: { bar: { borderRadius: 10, columnWidth: '35%' } } }
                }
            ]
        };
    }

    function buildEfficiencyChartConfig(d) {
        const eff = calculateEfficiency(d.estimatedHour, d.totalLogged);
        const cardColor = (typeof config !== 'undefined') ? config.colors.white : '#fff';
        const headingColor = (typeof config !== 'undefined') ? config.colors.headingColor : '#566a7f';

        return {
            chart: {
                height: 240,
                type: 'radialBar'
            },
            plotOptions: {
                radialBar: {
                    size: 150,
                    offsetY: 10,
                    startAngle: -150,
                    endAngle: 150,
                    hollow: {
                        size: '70%'
                    },
                    track: {
                        background: cardColor,
                        strokeWidth: '100%'
                    },
                    dataLabels: {
                        name: {
                            offsetY: 15,
                            color: labelColor,
                            fontSize: '15px',
                            fontWeight: '600',
                            fontFamily: 'Public Sans'
                        },
                        value: {
                            offsetY: -25,
                            color: headingColor,
                            fontSize: '22px',
                            fontWeight: '700',
                            fontFamily: 'Public Sans'
                        }
                    }
                }
            },
            colors: [eff.hexColor],
            fill: {
                type: 'gradient',
                gradient: {
                    shade: 'dark',
                    shadeIntensity: 0.5,
                    gradientToColors: [eff.hexColor],
                    inverseColors: true,
                    opacityFrom: 1,
                    opacityTo: 0.6,
                    stops: [30, 70, 100]
                }
            },
            stroke: {
                lineCap: 'round'
            },
            series: [Math.min(Math.round(eff.score), 100)],
            labels: ['Efficiency'],
            responsive: [
                {
                    breakpoint: 1200,
                    options: {
                        chart: { height: 200 }
                    }
                }
            ]
        };
    }

    // --- Validation Logic ---
    function validate() {
        const d = state.taskData;
        const reasons = [];
        let isHardBlocked = false;
        let isSoftGuarded = false;

        // 1. HARD BLOCKS (Strict)
        if (d.hasOpenSubtasks) {
            isHardBlocked = true;
            reasons.push({ type: 'hard', icon: 'bx-error-circle', msg: "Cannot finish: Active sub-tasks remaining." });
        }
        if (d.checklistCompleted < d.checklistTotal) {
            isHardBlocked = true;
            reasons.push({ type: 'hard', icon: 'bx-list-ul', msg: `Cannot finish: ${d.checklistTotal - d.checklistCompleted} mandatory checklist items open.` });
        }

        // 2. SOFT GUARDS (Quality)
        const notesValid = $('#ftCompletionNotes').val().trim().length > 10;
        const achievementsValid = state.achievements.length > 0;
        const nextStepsValid = state.nextSteps.length > 0 || $('#ftScheduleMeetingSwitch').is(':checked');
        const outcomeValid = state.outcomes.files.length > 0 || (state.outcomes.text.title && state.outcomes.text.content) || state.outcomes.links.length > 0;

        if (!notesValid) {
            isSoftGuarded = true;
            reasons.push({ type: 'soft', icon: 'bx-info-circle', msg: "Recommendation: Detailed completion notes are missing." });
        }
        if (!achievementsValid) {
            isSoftGuarded = true;
            reasons.push({ type: 'soft', icon: 'bx-trophy', msg: "Recommendation: Identify at least one key achievement." });
        }
        if (!nextStepsValid) {
            isSoftGuarded = true;
            reasons.push({ type: 'soft', icon: 'bx-fast-forward', msg: "Recommendation: No follow-up actions or meetings defined." });
        }
        if (!outcomeValid) {
            isSoftGuarded = true;
            reasons.push({ type: 'soft', icon: 'bx-cloud-upload', msg: "Notice: No formal outcome / deliverable attached." });
        }

        state.validation = {
            hardBlocked: isHardBlocked,
            reasons: reasons,
            canComplete: !isHardBlocked
        };

        updateQualityUI();
    }

    function updateQualityUI() {
        const $container = $('#ftQualityStatusContainer');
        const $finishBtn = $('#btnFinishWorkspace');
        const $prompt = $('#softGuardPrompt');
        const $guardInfo = $('#ftGuardrailInfo');

        $container.empty();
        $guardInfo.empty();

        if (state.validation.reasons.length === 0) {
            $container.html(`
                <div class="alert alert-label-success animate__animated animate__fadeIn mb-0">
                    <div class="d-flex align-items-center">
                        <i class="bx bx-check-double fs-4 me-2"></i>
                        <div class="fw-bold">All Quality Standards Met! Ready to Archive Workspace.</div>
                    </div>
                </div>
            `);
            $finishBtn.prop('disabled', false).removeClass('btn-label-primary', 'btn-warning').addClass('btn-primary');
            $prompt.addClass('d-none');
            return;
        }

        const isHard = state.validation.hardBlocked;
        const softOnly = state.validation.reasons.every(r => r.type === 'soft');

        // Main Alerts
        state.validation.reasons.forEach(r => {
            $container.append(`
                <div class="alert alert-label-${r.type === 'hard' ? 'danger' : 'warning'} py-2 mb-1 animate__animated animate__fadeInRight">
                    <div class="d-flex align-items-center">
                        <i class="bx ${r.icon} fs-4 me-2"></i>
                        <span class="small fw-semibold">${r.msg}</span>
                    </div>
                </div>
            `);
        });

        // Sidebar Summary
        if (isHard) {
            $guardInfo.html(`<p class="text-danger small fw-bold mb-0 lh-1"><i class="bx bx-block me-1"></i> Blocked by Critical Rules</p>`);
            $finishBtn.prop('disabled', true).removeClass('btn-primary btn-warning').addClass('btn-label-primary');
            $prompt.addClass('d-none');
        } else {
            $guardInfo.html(`<p class="text-warning small fw-bold mb-0 lh-1"><i class="bx bx-info-circle me-1"></i> Quality Recommendations</p>`);
            $finishBtn.prop('disabled', false).removeClass('btn-primary btn-label-primary').addClass('btn-warning');
            $prompt.removeClass('d-none');
        }
    }

    // --- Renderers ---
    function renderAchievements() {
        const $el = $('#ftAchievementsList');
        $el.empty();
        state.achievements.forEach((ach, i) => {
            $el.append(`<span class="badge bg-label-primary py-2 px-3 animate__animated animate__zoomIn">
                ${ach} <i class="bx bx-x ms-1 pointer" onclick="FinishWorkspace.removeItem('achievements', ${i})"></i>
            </span>`);
        });
    }

    function renderChallenges() {
        const $el = $('#ftChallengesList');
        $el.empty();
        state.challenges.forEach((ch, i) => {
            $el.append(`<span class="badge bg-label-danger py-2 px-3 animate__animated animate__zoomIn">
                ${ch} <i class="bx bx-x ms-1 pointer" onclick="FinishWorkspace.removeItem('challenges', ${i})"></i>
            </span>`);
        });
    }

    function renderLearnings() {
        const $el = $('#ftLearningsList');
        $el.empty();
        state.learnings.forEach((l, i) => {
            $el.append(`<span class="badge bg-label-info py-2 px-3 animate__animated animate__zoomIn">
                ${l} <i class="bx bx-x ms-1 pointer" onclick="FinishWorkspace.removeItem('learnings', ${i})"></i>
            </span>`);
        });
    }

    function renderNextSteps() {
        const $el = $('#ftNextStepList');
        const $empty = $('#nextStepsEmpty');

        if (state.nextSteps.length === 0) { $empty.removeClass('d-none'); return; }

        $empty.addClass('d-none');
        $el.find('.list-group-item').remove();

        state.nextSteps.forEach((s, i) => {
            $el.append(`
                <div class="list-group-item d-flex justify-content-between align-items-center border-bottom bg-transparent px-0 animate__animated animate__fadeInUp">
                    <div class="d-flex align-items-center">
                        <div class="avatar avatar-sm me-3">
                            <span class="avatar-initial rounded bg-label-primary"><i class="bx bx-list-check"></i></span>
                        </div>
                        <div>
                            <p class="mb-0 fw-bold small text-dark">${s.name}</p>
                            <small class="text-muted">Derived from current workspace</small>
                        </div>
                    </div>
                    <button class="btn btn-sm btn-icon border-0" onclick="FinishWorkspace.removeItem('nextSteps', ${i})">
                        <i class="bx bx-trash text-danger"></i>
                    </button>
                </div>
            `);
        });
    }

    function renderLinks() {
        const $el = $('#ftLinksList');
        $el.empty();
        state.outcomes.links.forEach((link, i) => {
            $el.append(`
                <div class="list-group-item d-flex justify-content-between align-items-center border px-2 py-1 rounded mb-1 bg-lighter small">
                    <div class="text-truncate me-3"><i class="bx bx-link me-1"></i> <a href="${link}" target="_blank">${link}</a></div>
                    <i class="bx bx-trash text-danger pointer" onclick="FinishWorkspace.removeItem('links', ${i})"></i>
                </div>
            `);
        });
    }

    function addFileToUI(file) {
        $('#ftFileList').append(`
            <div class="col-md-4 file-item animate__animated animate__fadeIn">
                <div class="card h-100 bg-lighter border shadow-none">
                    <div class="card-body p-2 d-flex align-items-center">
                        <i class="bx bx-file-blank fs-2 text-muted me-2"></i>
                        <div class="flex-grow-1 overflow-hidden">
                            <h6 class="mb-0 small fw-bold text-truncate">${file.name}</h6>
                            <small class="text-muted">${(file.size / 1024).toFixed(1)} KB</small>
                        </div>
                        <i class="bx bx-trash text-danger pointer ms-2" onclick="FinishWorkspace.removeFile('${file.name}')"></i>
                    </div>
                </div>
            </div>
        `);
    }

    function renderTimerHistory(sessions) {
        const $table = $('#ftTimerHistoryTable');
        $table.empty();
        sessions.forEach((s, i) => {
            $table.append(`
                <tr>
                    <td><span class="badge bg-label-primary">${i + 1}</span></td>
                    <td>${s.endPretty}</td>
                    <td class="text-end fw-bold">${s.duration}</td>
                </tr>
            `);
        });
    }

    // --- Actions ---
    async function handleCompleteAction(force = false) {
        if (state.validation.hardBlocked) return;

        const $btn = $('#btnFinishWorkspace');
        const oldContent = $btn.html();

        $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Closing...');

        const payload = {
            taskId: state.taskId,
            notes: $('#ftCompletionNotes').val(),
            achievements: state.achievements,
            challenges: state.challenges,
            learnings: state.learnings,
            nextSteps: state.nextSteps,
            meeting: $('#ftScheduleMeetingSwitch').is(':checked') ? {
                agenda: $('#ftMeetingAgenda').val(),
                date: $('#ftMeetingDate').val(),
                time: $('#ftMeetingTime').val()
            } : null,
            outcomes: {
                text: state.outcomes.text,
                linkCount: state.outcomes.links.length,
                fileCount: state.outcomes.files.length,
                metadata: {
                    archivedAt: new Date().toISOString(),
                    version: "1.0",
                    isForced: force
                }
            }
        };

        try {
            const result = await simulateApi(payload);
            if (result.success) {
                showToast("Workspace archived successfully. Knowledge base updated.", "success");
                setTimeout(() => window.location.href = '/Calendar', 1200);
            }
        } catch (err) {
            showToast("Failed to finalize workspace", "error");
            $btn.prop('disabled', false).html(oldContent);
        }
    }

    function simulateApi(payload) {
        console.log("Finalizing workspace payload:", payload);
        return new Promise(resolve => setTimeout(() => resolve({ success: true }), 1500));
    }

    // --- Helpers ---
    function calculateEfficiency(est, logged) {
        const loggedMins = parseDurationToMinutes(logged);
        const estMins = (est || 0) * 60;

        if (loggedMins <= 0 || estMins <= 0) return { percent: '0%', multiplier: '0', bgLabelClass: 'bg-label-secondary', score: 0, hexColor: '#8592a3' };

        const score = (estMins / loggedMins) * 100;
        const multiplier = (estMins / loggedMins).toFixed(1);

        let bgLabel = 'bg-label-success', hex = '#71dd37';
        if (score < 80) { bgLabel = 'bg-label-danger'; hex = '#ff3e1d'; }
        else if (score < 100) { bgLabel = 'bg-label-warning'; hex = '#ffab00'; }

        return {
            percent: Math.round(score) + '%',
            multiplier: multiplier,
            bgLabelClass: bgLabel,
            score: score,
            hexColor: hex
        };
    }

    function parseDurationToMinutes(str) {
        if (!str) return 0;
        let total = 0;
        const h = str.match(/(\d+)h/);
        const m = str.match(/(\d+)m/);
        const s = str.match(/(\d+)s/);
        if (h) total += parseInt(h[1]) * 60;
        if (m) total += parseInt(m[1]);
        if (s) total += (parseInt(s[1]) / 60);
        return total;
    }

    function showToast(msg, type) {
        if (typeof window.showToast === 'function') window.showToast(msg, type);
        else alert(`${type.toUpperCase()}: ${msg}`);
    }


    // --- Public Exposure ---
    return {
        init: init,
        initFinishTaskPage: initFinishTaskPage,
        initSessionsChart: initSessionsChart,
        initTimeSpentChart: initTimeSpentChart,
        initEstimateChart: initEstimateChart,
        initEfficiencyChart: initEfficiencyChart,
        removeItem: (listKey, index) => {
            if (listKey === 'links') state.outcomes.links.splice(index, 1);
            else state[listKey].splice(index, 1);

            if (listKey === 'achievements') renderAchievements();
            else if (listKey === 'challenges') renderChallenges();
            else if (listKey === 'learnings') renderLearnings();
            else if (listKey === 'nextSteps') renderNextSteps();
            else if (listKey === 'links') renderLinks();

            validate();
        },
        removeFile: (fileName) => {
            state.outcomes.files = state.outcomes.files.filter(f => f.name !== fileName);
            $('#ftFileList .file-item').each(function () {
                if ($(this).find('h6').text() === fileName) $(this).remove();
            });
            validate();
        }
    };
})();

// Document Ready
$(function () {
    const urlParams = new URLSearchParams(window.location.search);
    const taskId = urlParams.get('taskId');

    if (taskId) {
        FinishWorkspace.initFinishTaskPage(taskId);
    } else {
        FinishWorkspace.init();
    }
});
