'use strict';

/*
 * MOD-0024 — Task form logic (quick + detailed), Phase 1.
 *
 * The pure decision functions are exported on window.TaskForm so they are unit-testable without a DOM:
 *   visibleFieldsFor(target)   — which assignment fields apply (pack §12 K5)
 *   formatPositionLabel(row)   — "QA Specialist — Facility A" (pack §12 K4)
 *   buildCreatePayload(draft)  — the API body; never sends lifecycle or spentHours
 *   readDraft / writeDraft     — one shared draft so quick ↔ detailed loses nothing (pack §12 K9)
 */
(function (global) {
    const DRAFT_STORAGE_KEY = 'tasks.create-draft';

    const TARGET = {
        SELF: 'SelfAssigned',
        PERSON: 'Person',
        POOL: 'PositionPool'
    };

    /*
     * Which assignment fields are relevant for a target.
     * A pool task has NO assignee (it is claimed later) and a person task has no pool position — sending the
     * wrong one is rejected by the API, so the form must not offer it.
     */
    const visibleFieldsFor = (target) => {
        switch (target) {
            case TARGET.PERSON:
                return { assignee: true, poolPosition: false, organizationUnit: true };
            case TARGET.POOL:
                return { assignee: false, poolPosition: true, organizationUnit: true };
            case TARGET.SELF:
            default:
                // Self-assigned resolves both the assignee and the unit server-side from the actor.
                return { assignee: false, poolPosition: false, organizationUnit: false };
        }
    };

    /*
     * A position label MUST carry its organization unit: two facilities can each own a "QA Specialist", and an
     * unlabelled entry is how pooled work silently reaches the wrong site.
     */
    const formatPositionLabel = (row) => {
        if (!row) { return ''; }
        const position = row.positionName || row.positionCode || '';
        const unit = row.organizationUnitName || row.organizationUnitCode || '';
        return unit ? `${position} — ${unit}` : position;
    };

    const trimOrNull = (value) => {
        const text = (value === undefined || value === null) ? '' : String(value).trim();
        return text.length > 0 ? text : null;
    };

    const parseTags = (value) => {
        if (Array.isArray(value)) { return value; }
        return String(value || '')
            .split(',')
            .map((t) => t.trim())
            .filter((t) => t.length > 0);
    };

    const parseNumberOrNull = (value) => {
        const text = trimOrNull(value);
        if (text === null) { return null; }
        const parsed = Number(text);
        return Number.isFinite(parsed) ? parsed : null;
    };

    /*
     * Build the create body. Deliberately absent, because the server owns them:
     *   - lifecycle  → the system decides the initial state (an approval-gated task is not startable)
     *   - spentHours → always 0 on a new task; it only moves through execution
     */
    const buildCreatePayload = (draft) => {
        const target = draft.assignmentTarget || TARGET.SELF;
        const visible = visibleFieldsFor(target);

        return {
            title: trimOrNull(draft.title),
            description: trimOrNull(draft.description),
            priority: draft.priority || 'Medium',
            assignmentTarget: target,
            // Only ever send the field that belongs to the chosen target.
            assigneeUserId: visible.assignee ? trimOrNull(draft.assigneeUserId) : null,
            poolPositionId: visible.poolPosition ? trimOrNull(draft.poolPositionId) : null,
            organizationUnitId: trimOrNull(draft.organizationUnitId),
            dueAt: trimOrNull(draft.dueAt),
            startAt: trimOrNull(draft.startAt),
            plannedDate: trimOrNull(draft.plannedDate),
            estimateHours: parseNumberOrNull(draft.estimateHours),
            tags: parseTags(draft.tags),
            reviewRequired: !!draft.reviewRequired,
            approvalRequired: !!draft.approvalRequired,
            approvalManagerUserId: draft.approvalRequired ? trimOrNull(draft.approvalManagerUserId) : null,
            emailNotificationsEnabled: draft.emailNotificationsEnabled !== false,
            delegationAllowed: !!draft.delegationAllowed,
            fieldValues: Array.isArray(draft.fieldValues) ? draft.fieldValues : [],
            watchers: Array.isArray(draft.watchers) ? draft.watchers : []
        };
    };

    /*
     * Client-side pre-checks that mirror the server's validator, so the user is told immediately rather than
     * after a round trip. The server remains authoritative.
     */
    const validateDraft = (draft) => {
        const errors = [];
        const target = draft.assignmentTarget || TARGET.SELF;
        const visible = visibleFieldsFor(target);

        if (!trimOrNull(draft.title)) { errors.push('title'); }
        // A due date is required for all three targets, pool included.
        if (!trimOrNull(draft.dueAt)) { errors.push('dueAt'); }
        if (visible.assignee && !trimOrNull(draft.assigneeUserId)) { errors.push('assigneeUserId'); }
        if (visible.poolPosition && !trimOrNull(draft.poolPositionId)) { errors.push('poolPositionId'); }
        if (draft.approvalRequired && !trimOrNull(draft.approvalManagerUserId)) {
            errors.push('approvalManagerUserId');
        }

        return { valid: errors.length === 0, errors };
    };

    // ── Shared draft: quick mode and the detailed form are the SAME draft (pack §12 K9 / DEV-1) ──
    const writeDraft = (draft, storage) => {
        const store = storage || global.sessionStorage;
        if (!store) { return; }
        try {
            store.setItem(DRAFT_STORAGE_KEY, JSON.stringify(draft || {}));
        } catch (_) { /* storage unavailable: the form still works, it just cannot hand over */ }
    };

    const readDraft = (storage) => {
        const store = storage || global.sessionStorage;
        if (!store) { return null; }
        try {
            const raw = store.getItem(DRAFT_STORAGE_KEY);
            return raw ? JSON.parse(raw) : null;
        } catch (_) {
            return null;
        }
    };

    const clearDraft = (storage) => {
        const store = storage || global.sessionStorage;
        if (!store) { return; }
        try { store.removeItem(DRAFT_STORAGE_KEY); } catch (_) { /* noop */ }
    };

    // ── DOM wiring (thin; all decisions come from the pure helpers above) ──
    const applyTargetVisibility = (root, target) => {
        const scope = root || global.document;
        if (!scope || !scope.querySelectorAll) { return; }

        const visible = visibleFieldsFor(target);
        Object.keys(visible).forEach((field) => {
            scope.querySelectorAll(`[data-task-field="${field}"]`).forEach((el) => {
                // CSS class toggle, never an inline style (FG-003).
                el.classList.toggle('d-none', !visible[field]);
                const input = el.querySelector('input, select, textarea');
                if (input) { input.disabled = !visible[field]; }
            });
        });
    };

    const renderPositionOptions = (selectEl, rows) => {
        if (!selectEl) { return; }
        selectEl.innerHTML = '';
        (rows || []).forEach((row) => {
            const option = global.document.createElement('option');
            option.value = row.positionId;
            // The unit label is the whole point — see formatPositionLabel.
            option.textContent = formatPositionLabel(row);
            selectEl.appendChild(option);
        });
    };

    /*
     * A person label MUST carry position AND unit: two people holding "QA Specialist" in different facilities are
     * otherwise indistinguishable — the position picker's trap, transposed onto people.
     * The user id is NEVER shown; when the name cannot be resolved the caller supplies a fallback label.
     */
    const formatPersonLabel = (row, nameUnavailableLabel) => {
        if (!row) { return ''; }
        const name = row.displayName || nameUnavailableLabel || '';
        const parts = [name, row.positionName || row.positionCode, row.organizationUnitName || row.organizationUnitCode];
        return parts.filter((part) => part && String(part).trim().length > 0).join(' — ');
    };

    /*
     * Fill a person <select>. An empty list is a REAL state — nobody in the tenant holds a position — and gets an
     * explanation rather than a silently empty dropdown the user cannot interpret.
     */
    const renderPersonOptions = (selectEl, rows, labels) => {
        if (!selectEl) { return; }
        const text = labels || {};
        selectEl.innerHTML = '';

        if (!rows || rows.length === 0) {
            const empty = global.document.createElement('option');
            empty.value = '';
            empty.textContent = text.empty || '';
            empty.disabled = true;
            empty.selected = true;
            selectEl.appendChild(empty);
            selectEl.disabled = true;
            return;
        }

        selectEl.disabled = false;
        const placeholder = global.document.createElement('option');
        placeholder.value = '';
        placeholder.textContent = text.placeholder || '';
        selectEl.appendChild(placeholder);

        rows.forEach((row) => {
            const option = global.document.createElement('option');
            option.value = row.userId;
            option.textContent = formatPersonLabel(row, text.nameUnavailable);
            selectEl.appendChild(option);
        });
    };

    global.TaskForm = {
        DRAFT_STORAGE_KEY,
        TARGET,
        visibleFieldsFor,
        formatPositionLabel,
        formatPersonLabel,
        buildCreatePayload,
        validateDraft,
        readDraft,
        writeDraft,
        clearDraft,
        applyTargetVisibility,
        renderPositionOptions,
        renderPersonOptions
    };
})(typeof window !== 'undefined' ? window : globalThis);
