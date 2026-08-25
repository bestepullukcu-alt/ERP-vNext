'use strict';

/*
 * MOD-0024 — read-only task detail. The Task Center stays the personal ACTION surface; this page is the module's
 * own record view.
 */
(function (global) {
    const t = (key) => global.TasksL10n?.t?.(key) ?? key;
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

    /*
     * ── THE PRODUCT'S OWN READ-ONLY FIELD, COPIED FROM THE GOLDEN REFERENCE ──────────────────────────────
     *
     * MEASURED before, side by side with `GoldenReferenceCompact/Details`:
     *     golden : 12 × `.backbone-preview-field`, 4 × `.card.backbone-preview-section`, `col-md-6`
     *     here   :  0 ×  …                          0 × …                                 no columns
     * This page drew a `<dl class="row">` — a definition list with a 3/9 split, a shape the product uses
     * nowhere else. Arriving here from the Task Center felt like leaving the product, which is what the
     * owner reported.
     *
     * The markup below is the golden reference's, element for element: a glyph, then the label above and the
     * value beneath it. Nothing was designed here.
     *
     * ⚠ AN EMPTY FIELD STILL DRAWS, showing "-" — that is the golden reference's own behaviour
     * (`string.IsNullOrWhiteSpace(...) ? "-" : ...`). A record view whose rows appear and disappear by content
     * makes two records of the same type look like two different kinds of thing.
     */
    const field = (icon, label, value) => `
        <div class="col-12 col-md-6">
            <div class="backbone-preview-field">
                <i class="bx ${icon}"></i>
                <div>
                    <div class="backbone-preview-label">${esc(label)}</div>
                    <div class="backbone-preview-value mt-1">${value == null || value === '' ? '-' : esc(value)}</div>
                </div>
            </div>
        </div>`;

    const boot = async () => {
        const host = document.getElementById('taskDetails');
        const taskId = host?.getAttribute('data-task-id');
        if (!host || !taskId) { return; }

        const result = await global.TasksApi.get(taskId);
        // The host is now the Razor section (`.backbone-preview-section`), not a bare `.card-body`.
        const body = host.querySelector('.backbone-preview-section') || host.querySelector('.card-body');
        if (!body) { return; }

        if (!result.ok) {
            const message = result.status === 403 ? t('errorNoAccess') : t('errorUnavailable');
            body.innerHTML = `<h6 class="mb-0">${esc(message)}</h6>`;
            return;
        }

        const task = result.data;
        /*
         * ⚠ THE TITLE AND THE EDIT LINK ARE NOT DRAWN HERE ANY MORE — they moved into the Razor header, where
         * the golden reference keeps them, together with the breadcrumb and a Back button this page never had.
         * The old header also carried a button labelled "Kaydet" that was an `<a href=".../Edit">`: a Save
         * button on a read-only page that saved nothing. It says "Düzenle" now, in Razor.
         */
        const assignmentKey = task.assignmentTarget === 'PositionPool' ? 'Pool'
            : task.assignmentTarget === 'Person' ? 'Person' : 'Self';
        body.innerHTML = `
            <h6 class="text-uppercase text-heading fw-semibold mb-4">${esc(task.title)}</h6>
            <div class="row g-4">
                ${field('bx-flag', t('columnStatus'), t(`lifecycle${task.lifecycle}`))}
                ${field('bx-up-arrow-alt', t('columnPriority'), t(`priority${task.priority}`))}
                ${field('bx-user-pin', t('columnAssignment'), t(`target${assignmentKey}`))}
                ${field('bx-calendar', t('columnDueAt'), (task.dueAt || '').slice(0, 10))}
                ${field('bx-time-five', t('fieldEstimateHours'), task.estimateHours)}
                ${field('bx-timer', t('fieldSpentHours'), task.spentHours)}
                ${field('bx-hourglass', t('fieldRemainingHours'), task.remainingHours)}
                ${field('bx-detail', t('fieldDescription'), task.description)}
            </div>`;
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})(typeof window !== 'undefined' ? window : globalThis);
