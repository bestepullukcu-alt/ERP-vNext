'use strict';

/*
 * MOD-0024 — read-only task detail. The Task Center stays the personal ACTION surface; this page is the module's
 * own record view.
 */
(function (global) {
    const t = (key) => global.TasksL10n?.t?.(key) ?? key;
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

    const row = (label, value) => value == null || value === ''
        ? ''
        : `<dt class="col-sm-3">${esc(label)}</dt><dd class="col-sm-9">${esc(value)}</dd>`;

    const boot = async () => {
        const host = document.getElementById('taskDetails');
        const taskId = host?.getAttribute('data-task-id');
        if (!host || !taskId) { return; }

        const result = await global.TasksApi.get(taskId);
        const body = host.querySelector('.card-body');
        if (!body) { return; }

        if (!result.ok) {
            const message = result.status === 403 ? t('errorNoAccess') : t('errorUnavailable');
            body.innerHTML = `<h6 class="mb-0">${esc(message)}</h6>`;
            return;
        }

        const task = result.data;
        body.innerHTML = `
            <div class="d-flex justify-content-between align-items-start mb-3">
                <h5 class="mb-0">${esc(task.title)}</h5>
                <a class="btn btn-sm btn-label-primary" href="/Tasks/${esc(task.id)}/Edit">${esc(t('actionSave'))}</a>
            </div>
            <dl class="row mb-0">
                ${row(t('columnStatus'), t(`lifecycle${task.lifecycle}`))}
                ${row(t('columnPriority'), t(`priority${task.priority}`))}
                ${row(t('columnAssignment'), t(`target${task.assignmentTarget === 'PositionPool' ? 'Pool'
                    : task.assignmentTarget === 'Person' ? 'Person' : 'Self'}`))}
                ${row(t('columnDueAt'), (task.dueAt || '').slice(0, 10))}
                ${row(t('fieldEstimateHours'), task.estimateHours)}
                ${row(t('fieldSpentHours'), task.spentHours)}
                ${row(t('fieldRemainingHours'), task.remainingHours)}
                ${row(t('fieldDescription'), task.description)}
            </dl>`;
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})(typeof window !== 'undefined' ? window : globalThis);
