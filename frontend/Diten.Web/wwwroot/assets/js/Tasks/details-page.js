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

    /*
     * ── DCP-005 slice 3 — "according to" ────────────────────────────────────────────────────────────────
     *
     * ⚠ DRAWN ONLY WHEN THERE IS SOMETHING TO DRAW, and that is the one place this page departs from the
     * golden reference's "an empty field still shows a dash" rule — on purpose, and the pack names the rule:
     * DCP-004, do not announce a capability there is no data for. An empty "According to" card on every task in
     * the product would tell every reader that citations exist and that this task has none, which is a claim
     * about the task rather than about the feature.
     *
     * ⚠ EVERY VALUE HERE IS THE FROZEN ONE. Nothing on this path asks the register anything; a read that
     * re-resolved a title would undo the freeze on every page load and would look exactly like this function.
     */
    const documentCard = (references) => {
        if (!Array.isArray(references) || references.length === 0) { return ''; }

        const rows = references.map((r) => `
            <li class="tasks-docref-frozen">
                <span class="tasks-docref-code">${esc(r.documentCode)}</span>
                <span class="tasks-docref-title">${esc(r.title)}</span>
                ${r.documentVersion ? `<span class="tasks-docref-version">${esc(r.documentVersion)}</span>` : ''}
                ${r.status ? `<span class="tasks-docref-status">${esc(r.status)}</span>` : ''}
                <span class="tasks-docref-date">${esc(t('docRefReferencedAt'))} ${
                    esc(String(r.referencedAt || '').slice(0, 10))}</span>
            </li>`).join('');

        return `
            <section class="tasks-docref-card mt-4">
                <h6 class="text-uppercase text-heading fw-semibold mb-3">${esc(t('docRefSectionTitle'))}</h6>
                <ul class="list-unstyled mb-0">${rows}</ul>
            </section>`;
    };

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
            </div>
            ${documentCard(task.documentReferences)}`;
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})(typeof window !== 'undefined' ? window : globalThis);
