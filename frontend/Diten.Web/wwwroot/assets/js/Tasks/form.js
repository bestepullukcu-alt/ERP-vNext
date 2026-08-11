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
            // Dropped with the requirement, exactly as the approval manager is: sending a reviewer for a task
            // that needs no review would store a candidate nothing will ever route to.
            reviewerCandidateUserId: draft.reviewRequired ? trimOrNull(draft.reviewerCandidateUserId) : null,
            approvalRequired: !!draft.approvalRequired,
            approvalManagerUserId: draft.approvalRequired ? trimOrNull(draft.approvalManagerUserId) : null,
            emailNotificationsEnabled: draft.emailNotificationsEnabled !== false,
            /*
             * BL-065 — the per-event preference and the reminder lead time.
             *
             * Dropped with the channel, exactly as the reviewer is dropped with the review requirement: with the
             * master switch off the card is hidden, so whatever the boxes hold is not a choice anyone made and
             * storing it would record a preference nobody expressed. Null then means "not chosen", which is the
             * state every task written before this field carries — and which the server reads as "everything".
             */
            notifyOnEvents: draft.emailNotificationsEnabled === false
                ? null
                : (Array.isArray(draft.notifyOnEvents) ? draft.notifyOnEvents : null),
            reminderLeadDays: draft.emailNotificationsEnabled === false
                ? null
                : parseNumberOrNull(draft.reminderLeadDays),
            delegationAllowed: !!draft.delegationAllowed,
            fieldValues: Array.isArray(draft.fieldValues) ? draft.fieldValues : [],
            watchers: toWatcherRequests(draft.watchers)
        };
    };

    /*
     * The EDIT body: the create body plus the version the edit was based on.
     *
     * It exists as its own function because of one asymmetry the server cares about. An update tells "I am not
     * editing the notification preferences" from "I am clearing them" by whether `notifyOnEvents` travels, and
     * inside such an edit the lead time is applied verbatim — null included. The form always renders the card, so
     * an edit always carries the list, and "no reminder" actually switches the reminder off. Built inline at the
     * call site, that rule was one spread operator away from being lost, and losing it is silent: the save
     * succeeds and the reminder keeps arriving.
     */
    const buildUpdatePayload = (draft, expectedVersion) => ({
        ...buildCreatePayload(draft),
        expectedVersion: Number(expectedVersion) || 1
    });

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
        // Mirrors the server's rule so the user is told before the round trip. The server stays authoritative:
        // it refuses with REVIEW_REVIEWER_REQUIRED whatever the client believes.
        if (draft.reviewRequired && !trimOrNull(draft.reviewerCandidateUserId)) {
            errors.push('reviewerCandidateUserId');
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
    const renderPersonOptions = (selectEl, rows, labels, options) => {
        if (!selectEl) { return; }
        const text = labels || {};
        const multiple = !!(options && options.multiple);
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
        // A MULTI-select gets no placeholder row: in a list box a placeholder is just another selectable line,
        // and selecting it would post an empty identity among the real ones. Single-select keeps it — there it
        // is the "nothing chosen" state, which is a legitimate answer for an optional field.
        if (!multiple) {
            const placeholder = global.document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = text.placeholder || '';
            selectEl.appendChild(placeholder);
        }

        rows.forEach((row) => {
            const option = global.document.createElement('option');
            option.value = row.userId;
            option.textContent = formatPersonLabel(row, text.nameUnavailable);
            selectEl.appendChild(option);
        });
    };

    /*
     * Watcher rows are objects on the wire — TaskWatcherRequest(UserId, Role, PositionId) — but a <select> holds
     * identities. This selects the stored ones in a picker that has already been filled from the people lookup,
     * so the edit form shows NAMES for identities it stored, rather than the raw list the API answered with.
     * A stored identity that no longer holds a position is not in the list any more; it is skipped rather than
     * invented, which is the same rule the record pickers follow.
     */
    const selectWatchers = (selectEl, watcherRows) => {
        if (!selectEl) { return []; }
        const wanted = new Set((watcherRows || [])
            .map((row) => (typeof row === 'string' ? row : row && row.userId))
            .filter(Boolean)
            .map(String));

        const matched = [];
        Array.from(selectEl.options).forEach((option) => {
            option.selected = wanted.has(option.value);
            if (option.selected) { matched.push(option.value); }
        });
        return matched;
    };

    // What the form sends for a person it is only WATCHING. The other watcher role (Consultant) is a different
    // capability with its own approval semantics (BL-053) and is deliberately not offered here.
    const WATCHER_ROLE = 'Watcher';

    /*
     * Accepts either the identities a multi-select yields or rows already in wire shape, and always returns wire
     * shape. Before this, readForm handed buildCreatePayload a comma-separated STRING while the payload only
     * forwarded arrays — so every watcher the user entered was dropped without a word.
     */
    const toWatcherRequests = (watchers) => {
        if (!Array.isArray(watchers)) { return []; }
        return watchers
            .map((entry) => {
                if (typeof entry === 'string') {
                    const userId = entry.trim();
                    return userId.length > 0 ? { userId, role: WATCHER_ROLE, positionId: null } : null;
                }
                if (entry && entry.userId) {
                    return {
                        userId: entry.userId,
                        role: entry.role || WATCHER_ROLE,
                        positionId: entry.positionId ?? null
                    };
                }
                return null;
            })
            .filter(Boolean);
    };

    /* ── Configurable fields (Phase 5) ────────────────────────────────────────────────────────────────────
     *
     * The form had RESERVED SPACE for these since Phase 1 (`#taskCustomFields`, born `d-none`) and nothing ever
     * filled it. Everything below is what fills it — and, just as importantly, what it REFUSES to fill.
     *
     * Two refusals, both deliberate:
     *   1. A value type with no control this round is not half-rendered. No placeholder box, no "coming soon" —
     *      the field is absent and the console says which one and why.
     *   2. An option-driven field whose source did not resolve is NOT shown as an empty dropdown. A selector
     *      nobody can fill is the same class of defect as a payload nobody reads (BL-050).
     */

    // Types whose value is TYPED by the user. Anything not listed has no control this round.
    const CUSTOM_FIELD_CONTROL_BY_TYPE = {
        Text: 'text',
        Number: 'number',
        Currency: 'currency',
        Percentage: 'percentage',
        Date: 'date',
        DateTime: 'datetime',
        Boolean: 'boolean',
        Link: 'link'
    };

    // Types whose value is CHOSEN from a list. `Status` needs a declared options source; `Person` is resolved
    // from the tenant's assignable people, the same list the assignee picker uses.
    const OPTION_DRIVEN_TYPES = new Set(['Text', 'Status']);

    /*
     * A source KIND, not a source key. The distinction matters: this file may know that some fields are backed
     * by another module's records, and must never know WHICH module — the day it names one is the day adding the
     * Product module means editing the form.
     */
    const MODULE_RECORD_KIND = 'ModuleRecord';

    // The one value type whose stored value IS an identity, so it is the only one a record source can back.
    const IDENTITY_VALUE_TYPE = 'Reference';

    const hasOptionsSource = (definition) =>
        !!definition
        && definition.optionsSourceKind
        && definition.optionsSourceKind !== 'None'
        && String(definition.optionsSourceKey || '').trim().length > 0;

    const isModuleRecordSource = (definition) =>
        hasOptionsSource(definition) && definition.optionsSourceKind === MODULE_RECORD_KIND;

    /*
     * Which control a definition gets, or null when this round renders none.
     *
     * `Reference` used to be absent entirely, and the reason was sound: it points at an arbitrary entity with no
     * generic resolver, so the only control we could offer was a raw GUID box the user cannot fill correctly.
     * A MODULE RECORD source is exactly that missing resolver — the definition now says which module owns the
     * values — so a Reference field that names one gets a searchable picker, and one that names none is still
     * omitted for the original reason.
     */
    const customFieldControlKind = (definition) => {
        if (!definition) { return null; }
        const type = definition.valueType;

        if (type === 'Person') { return 'person'; }

        if (isModuleRecordSource(definition)) {
            // The value stored is an identity. On a Number or a Date the server refuses it, so offering a
            // control here would only produce a refusal the user cannot act on.
            return type === IDENTITY_VALUE_TYPE ? 'record' : null;
        }

        if (hasOptionsSource(definition)) {
            // An option code is a string. Declaring a source on a numeric/date/boolean field would produce
            // values the server refuses at IsWellFormed, so the field is not rendered at all.
            return OPTION_DRIVEN_TYPES.has(type) ? 'select' : null;
        }

        // A Status with no source would be an EMPTY dropdown. That is the BL-050 defect, not a degraded control.
        if (type === 'Status') { return null; }

        return CUSTOM_FIELD_CONTROL_BY_TYPE[type] || null;
    };

    const customFieldLabel = (definition, translate) => {
        if (!definition) { return ''; }
        // A tenant's own words are rendered as typed; a SYSTEM field carries a resource key that must resolve
        // through the 7-language payload. Never the code — the code is not a label.
        if (definition.labelText) { return definition.labelText; }
        if (definition.labelResourceKey) {
            return (typeof translate === 'function')
                ? translate(definition.labelResourceKey)
                : definition.labelResourceKey;
        }
        return '';
    };

    const CONTROL_INPUT_TYPE = {
        text: 'text',
        number: 'number',
        currency: 'number',
        percentage: 'number',
        date: 'date',
        datetime: 'datetime-local',
        link: 'url'
    };

    /*
     * What ONE choice reads as. `choice.secondary` is TaskFieldOptionDto's optional disambiguating line — the
     * business key, and the unit where two facilities can each own a "QA Specialist".
     *
     * Shared by every option-driven kind on purpose: the short sources send no secondary, so they are unchanged,
     * and a record's key reaches the reader through the same line rather than a second formatter. What is never
     * shown is `choice.value` — that is the identity, and BL-049 is about exactly this line.
     */
    const optionText = (choice) => {
        const label = choice.label == null ? '' : String(choice.label);
        const secondary = choice.secondary == null ? '' : String(choice.secondary).trim();
        return secondary.length > 0 ? `${label} — ${secondary}` : label;
    };

    const buildCustomFieldControl = (definition, kind, options, labels) => {
        const doc = global.document;
        const text = labels || {};

        if (kind === 'select' || kind === 'person' || kind === 'boolean' || kind === 'record') {
            const select = doc.createElement('select');
            // `select2` here, not only in the .cshtml: these controls are BUILT, so markup-only styling would
            // leave every tenant-defined dropdown looking different from the form around it. enhanceSelects()
            // binds them after they are rendered.
            select.className = 'form-select select2';
            // Marks the control as one whose options are a PAGE of a larger set, not the whole set. Hydration
            // reads this: a value missing from a complete list is a bad value, a value missing from a page is
            // ordinary and must be added rather than dropped.
            /*
             * A record control holds ONE PAGE of a source that can have thousands of rows, so its search has to
             * reach the SERVER. That used to mean a hand-rolled search box above the control with select2's own
             * search switched off — two controls for one field. select2's `ajax` is the feature for precisely
             * this: the user types in select2's own box and select2 asks the server. This attribute is what
             * enhanceSelects keys that decision off, and it is also what hydration reads to tell "missing from a
             * page" (ordinary) from "missing from a complete list" (a bad value).
             */
            if (kind === 'record') {
                select.setAttribute('data-custom-field-record', '1');
                // The prompt the picker shows before anything is chosen. It was the old search box's placeholder
                // ("type to search"), which still says exactly the right thing now that the typing happens inside
                // the picker — so the string keeps its job rather than being orphaned.
                select.setAttribute('data-placeholder', text.recordSearchPlaceholder || text.optionPlaceholder || '');
            }

            const placeholder = doc.createElement('option');
            placeholder.value = '';
            placeholder.textContent = text.optionPlaceholder || '';
            select.appendChild(placeholder);

            const rows = kind === 'boolean'
                ? [{ value: 'true', label: text.booleanYes || '' }, { value: 'false', label: text.booleanNo || '' }]
                : (options || []);

            // `choice.value`/`choice.label` are TaskFieldOptionDto's own fields, and the BL-050 guard in
            // task-transition-contract.test.js reads this line against that record — a renamed field fails there
            // rather than as a picker full of value="" options nobody can submit.
            rows.forEach((choice) => {
                const option = doc.createElement('option');
                option.value = choice.value;
                option.textContent = optionText(choice);
                select.appendChild(option);
            });
            return select;
        }

        const input = doc.createElement('input');
        input.className = 'form-control';
        input.type = CONTROL_INPUT_TYPE[kind] || 'text';
        if (kind === 'text') { input.maxLength = 2000; }
        if (kind === 'number' || kind === 'currency' || kind === 'percentage') { input.step = 'any'; }
        if (kind === 'percentage') { input.min = '0'; input.max = '100'; }
        return input;
    };

    /*
     * Render the definitions into the row. Returns the codes ACTUALLY rendered so the caller can decide whether
     * the section has anything to show — an empty section is kept hidden, which is the behaviour the form
     * already had when there were no definitions at all.
     *
     * `optionsByCode` maps a definition code to its resolved [{value,label}] list. A code needing options and
     * missing from the map (or mapping to an empty list) is skipped, loudly.
     */
    const renderCustomFields = (container, definitions, optionsByCode, labels) => {
        if (!container) { return []; }
        const doc = global.document;
        const options = optionsByCode || {};
        const rendered = [];

        container.innerHTML = '';

        const live = (definitions || [])
            .filter((definition) => definition && definition.isActive !== false)
            .slice()
            .sort((a, b) =>
                String(a.section || '').localeCompare(String(b.section || ''))
                || (a.sortOrder || 0) - (b.sortOrder || 0)
                || String(a.code || '').localeCompare(String(b.code || '')));

        let currentSection = null;

        live.forEach((definition) => {
            const kind = customFieldControlKind(definition);
            if (!kind) {
                global.console?.warn?.(
                    `[TaskForm] field "${definition.code}" (${definition.valueType}) has no control in this `
                    + 'round and is NOT rendered. Configurable-field types still to build: Reference; and any '
                    + 'Status field without an options source.');
                return;
            }

            const needsOptions = kind === 'select' || kind === 'person' || kind === 'record';
            const resolved = options[definition.code];
            if (needsOptions && (!Array.isArray(resolved) || resolved.length === 0)) {
                global.console?.warn?.(
                    `[TaskForm] field "${definition.code}" is option-driven but its source `
                    + `(${definition.optionsSourceKind}/${definition.optionsSourceKey || '—'}) resolved to no `
                    + 'options; the field is NOT rendered rather than shown as an empty picker.');
                return;
            }

            // A section caption per group: sections are the administrator's own words, like labelText.
            const section = String(definition.section || '');
            if (section && section !== currentSection) {
                currentSection = section;
                const caption = doc.createElement('div');
                caption.className = 'col-12';
                const inner = doc.createElement('div');
                inner.className = 'text-muted small fw-medium';
                inner.textContent = section;
                caption.appendChild(inner);
                container.appendChild(caption);
            }

            const column = doc.createElement('div');
            // The SAME width the rest of the form uses for a field. At col-md-4 two configurable fields sat in a
            // row that everything above them filled with two — so the tenant's own fields were the one part of
            // the page that did not line up.
            column.className = 'col-md-6';

            const label = doc.createElement('label');
            label.className = 'form-label';
            label.textContent = customFieldLabel(definition, labels && labels.translate);
            if (definition.isRequired) {
                const mark = doc.createElement('span');
                mark.className = 'text-danger';
                mark.textContent = ' *';
                label.appendChild(mark);
            }

            const control = buildCustomFieldControl(definition, kind, resolved, labels);
            control.setAttribute('data-custom-field', definition.code);
            control.setAttribute('data-custom-field-type', definition.valueType);
            const controlId = `taskCustomField_${String(definition.code).replace(/[^A-Za-z0-9_-]/g, '_')}`;
            control.id = controlId;
            label.setAttribute('for', controlId);

            column.appendChild(label);

            /*
             * A record field used to get its OWN search input here, above the control: a source can hold five
             * thousand records and they must never all cross the wire, so the search had to reach the server and
             * select2's local one could not. That produced two controls for one field. The search box is gone —
             * enhanceSelects now hands the same server search to select2's `ajax`, so the user types in the
             * picker itself. `recordSearchPlaceholder` is still used, as that picker's prompt.
             */
            column.appendChild(control);
            container.appendChild(column);
            rendered.push(definition.code);
        });

        return rendered;
    };

    const customFieldControl = (container, code) =>
        container ? container.querySelector(`[data-custom-field="${code}"]`) : null;

    /*
     * Read the rendered controls back into the contract's field-value shape. An empty optional field is OMITTED
     * rather than sent as a blank: storing "" would put an empty row on the task that reads as "answered".
     */
    const readCustomFieldValues = (container, definitions) => {
        if (!container) { return []; }
        return (definitions || [])
            .filter((definition) => definition && definition.isActive !== false)
            .map((definition) => {
                const control = customFieldControl(container, definition.code);
                if (!control) { return null; }
                const value = String(control.value ?? '').trim();
                if (value.length === 0) { return null; }
                return {
                    definitionCode: definition.code,
                    valueType: definition.valueType,
                    value
                };
            })
            .filter(Boolean);
    };

    const isRecordControl = (control) =>
        !!control && control.getAttribute && control.getAttribute('data-custom-field-record') === '1';

    /*
     * Replace a record picker's options with a freshly searched page, keeping the CURRENT selection in the list.
     *
     * Dropping it would be the data-loss bug wearing a different hat: the user picks a department, types in the
     * search box to check something, and the value they already chose disappears from the control — so the save
     * posts a task without it. A selection survives its own search.
     */
    const renderRecordOptions = (container, code, rows, labels) => {
        const control = customFieldControl(container, code);
        if (!control) { return; }

        const doc = global.document;
        const text = labels || {};
        const selectedValue = control.value;
        const selectedText = control.selectedOptions && control.selectedOptions[0]
            ? control.selectedOptions[0].textContent
            : '';

        control.innerHTML = '';

        const placeholder = doc.createElement('option');
        placeholder.value = '';
        placeholder.textContent = text.optionPlaceholder || '';
        control.appendChild(placeholder);

        (rows || []).forEach((choice) => {
            const option = doc.createElement('option');
            option.value = choice.value;
            option.textContent = optionText(choice);
            control.appendChild(option);
        });

        if (!selectedValue) { return; }
        if (![...control.options].some((option) => option.value === selectedValue)) {
            const kept = doc.createElement('option');
            kept.value = selectedValue;
            kept.textContent = selectedText;
            control.appendChild(kept);
        }
        control.value = selectedValue;
    };

    /*
     * Put stored values back on an EDIT. Without this a read-only create becomes a data-losing edit: the form
     * would post back a payload with every configurable field emptied.
     *
     * `recordsByCode` carries the records the SERVER resolved for the identities already on the task. A record
     * picker only ever holds one PAGE of its source, so a task saved months ago routinely points at something
     * that page does not contain — and a <select> given a value with no matching <option> silently keeps its old
     * one. That is how an edit posts back a different department than it displayed.
     */
    const writeCustomFieldValues = (container, values, recordsByCode, labels) => {
        if (!container) { return; }
        const resolved = recordsByCode || {};
        const text = labels || {};

        (values || []).forEach((entry) => {
            if (!entry || !entry.definitionCode) { return; }
            const control = customFieldControl(container, entry.definitionCode);
            if (!control) { return; }

            const raw = entry.value == null ? '' : String(entry.value);
            const type = control.getAttribute('data-custom-field-type') || entry.valueType;

            if (isRecordControl(control) && raw.length > 0) {
                if (![...control.options].some((option) => option.value === raw)) {
                    const match = (resolved[entry.definitionCode] || [])
                        .find((row) => String(row.value) === raw);
                    const option = global.document.createElement('option');
                    option.value = raw;
                    /*
                     * A record the server could not resolve — deleted upstream, say — still has an identity on
                     * the task, and the user must NOT be shown it (BL-049). They are told the record is
                     * unavailable; the value itself is preserved so an unrelated edit does not silently clear it.
                     */
                    option.textContent = match ? optionText(match) : (text.recordUnavailable || '');
                    control.appendChild(option);
                }
                control.value = raw;
                return;
            }

            // The server stores a full timestamp; `input[type=date]` only accepts yyyy-MM-dd and silently
            // rejects anything else, which is how a hydrated date arrives EMPTY and is then saved away.
            if (type === 'Date') { control.value = raw.slice(0, 10); return; }
            if (type === 'DateTime') { control.value = raw.slice(0, 16); return; }
            control.value = raw;
        });
    };

    /*
     * A required field with no value blocks the save on the client. The server refuses it too — the two are
     * deliberately the same rule in two places, because a client-only rule is a suggestion.
     *
     * A definition this round does NOT render cannot be demanded: blocking on a field the user was never shown
     * would make the form unsavable with no way to fix it.
     */
    const validateCustomFields = (definitions, values) => {
        const supplied = new Set(
            (values || [])
                .filter((entry) => entry && String(entry.value ?? '').trim().length > 0)
                .map((entry) => entry.definitionCode));

        const errors = (definitions || [])
            .filter((definition) =>
                definition
                && definition.isActive !== false
                && definition.isRequired
                && customFieldControlKind(definition) !== null
                && !supplied.has(definition.code))
            .map((definition) => definition.code);

        return { valid: errors.length === 0, errors };
    };

    /*
     * Bind select2 to every `.select2` inside `root` that is not bound yet — the same wrap+dropdownParent shape
     * the golden reference form uses, so the two look identical.
     *
     * It takes a ROOT and is safe to call again because half this form is built after load: the static pickers
     * exist in the markup, the configurable ones are created by renderCustomFields, and binding once at
     * DOMContentLoaded would enhance only the first half — a fix that looks complete on screen until a tenant
     * defines a field. `select2-hidden-accessible` is select2's own marker for "already bound", so a second call
     * re-binds nothing.
     */
    /*
     * How long select2 waits after the last keystroke before asking the server. A request per keystroke turns a
     * five-thousand-row source into five thousand requests; this is the same 250ms the hand-rolled search box
     * used, kept rather than re-chosen.
     */
    const RECORD_SEARCH_DELAY_MS = 250;

    /*
     * WHICH CONTROLS ROUND-TRIP, stated once as a rule rather than per field:
     * a control is server-searched exactly when its options are a PAGE of a larger source — which is what
     * `data-custom-field-record` marks (a ModuleRecord-backed field). Everything else — a lookup list, the
     * people picker, priority, the assignment target — already holds every row it will ever have, so select2's
     * own local search is both correct and instant. Asking the server for those would be a round trip to filter
     * a list the browser is already holding.
     */
    const isServerSearched = (node) => !!node && node.getAttribute('data-custom-field-record') === '1';

    /*
     * The transport select2 uses for a server-searched control.
     *
     * Guarded by a sequence number, because a slow answer for "Kal" must not overwrite the answer for "Kalite"
     * that arrived first — an ordinary failure of search-as-you-type, and invisible until the picker shows
     * results for something the user already finished typing. select2's `delay` covers the debounce.
     */
    const recordSearchTransport = (node, code, searchRecords) => {
        let sequence = 0;
        return (params, success, failure) => {
            const mine = ++sequence;
            const term = (params && params.data && params.data.term) || '';
            Promise.resolve(searchRecords(code, term))
                .then((rows) => {
                    if (mine !== sequence) { return; }   // stale: the user has typed since.
                    success({
                        results: (rows || []).map((row) => ({ id: row.value, text: optionText(row) }))
                    });
                })
                .catch((error) => {
                    if (mine !== sequence) { return; }
                    if (typeof failure === 'function') { failure(error); }
                });
        };
    };

    const enhanceSelects = (root, options) => {
        const scope = root || global.document;
        if (!scope || typeof global.jQuery !== 'function') { return 0; }

        const searchRecords = options && options.searchRecords;
        const nodes = Array.from(scope.querySelectorAll('select.select2'))
            .filter((node) => !node.classList.contains('select2-hidden-accessible'));

        nodes.forEach((node) => {
            const $node = global.jQuery(node);
            $node.wrap('<div class="position-relative"></div>');
            const settings = { dropdownParent: $node.parent() };
            const placeholder = node.getAttribute('data-placeholder');
            if (placeholder) { settings.placeholder = placeholder; }

            if (isServerSearched(node) && typeof searchRecords === 'function') {
                const code = node.getAttribute('data-custom-field');
                settings.ajax = {
                    delay: RECORD_SEARCH_DELAY_MS,
                    transport: recordSearchTransport(node, code, searchRecords)
                };
            }

            $node.select2(settings);

            /*
             * THE NOTIFICATION BRIDGE, and it belongs here rather than beside each listener.
             *
             * select2 announces a choice the jQuery way — $(select).trigger('change') — and jQuery's trigger does
             * NOT run listeners added with addEventListener. So the moment a control became a select2, every
             * native `change` listener on it went deaf: choosing "Bir kişi" stopped opening the assignee picker,
             * and the same for the pool and both governance switches. Three listeners broke at once and nothing
             * failed, because the producer changed and the consumers were never told.
             *
             * Binding is therefore what guarantees delivery: a control cannot be enhanced here and stay silent,
             * so a fourth conditional field added tomorrow works without knowing any of this. The guard keeps a
             * plain native change from being re-broadcast — jQuery hears those too, and the pair would loop.
             */
            $node.on('change', (event) => {
                // A change that already came from the DOM has reached the native listeners on its own; only a
                // jQuery-SYNTHESISED one (which is what select2 fires) needs carrying across. jQuery marks the
                // difference itself: a wrapped native event has `originalEvent`, a triggered one does not. Using
                // that instead of a re-entrancy flag means the bridge cannot echo its own dispatch.
                if (event && event.originalEvent) { return; }
                node.dispatchEvent(new global.Event('change', { bubbles: true }));
            });
        });

        return nodes.length;
    };

    /*
     * BL-065 — put a stored preference back on the controls.
     *
     * NULL is not "nothing ticked": it means the owner never chose, and the server sends every event for such a
     * task. Showing an empty card for it would tell the user the opposite of what the system does, so null ticks
     * everything — which is also what saving it back then makes explicit.
     */
    const applyNotificationPreferences = (root, notifyOnEvents, reminderLeadDays) => {
        const scope = root || global.document;
        if (!scope || !scope.querySelectorAll) { return; }

        const chosen = Array.isArray(notifyOnEvents) ? notifyOnEvents : null;
        scope.querySelectorAll('[name="notifyOnEvents"]').forEach((box) => {
            box.checked = chosen === null ? true : chosen.includes(box.value);
        });

        const lead = scope.querySelector('[name="reminderLeadDays"]');
        if (lead) {
            // 0 is a real answer ("on the day"), so only null/undefined clears the control.
            lead.value = reminderLeadDays === null || reminderLeadDays === undefined
                ? ''
                : String(reminderLeadDays);
        }
    };

    /*
     * Tags as CHIPS, using the library the rest of the app already uses for exactly this (the tenant-security
     * screen's IP and country lists). A plain text box asked the user to know that commas separate tags and gave
     * no sign of what had been entered.
     *
     * `originalInputValueFormat` keeps the underlying <input> comma-separated, so parseTags — and therefore the
     * array the API receives — is unchanged. That is the same reason the security screen passes it.
     */
    const enhanceTags = (root) => {
        const scope = root || global.document;
        if (!scope || typeof global.Tagify !== 'function') { return 0; }

        const nodes = Array.from(scope.querySelectorAll('#taskTags'))
            .filter((node) => !node.__tagify);

        nodes.forEach((node) => {
            node.__tagify = new global.Tagify(node, {
                originalInputValueFormat: (values) => values.map((v) => v.value).join(',')
            });
        });

        return nodes.length;
    };

    /*
     * The date fields, the way the golden reference does them.
     *
     * A native <input type="date"> takes its display format from the OPERATING SYSTEM's locale, so an Arabic
     * page still rendered gg.aa.yyyy — the page's own language never entered into it. flatpickr renders the
     * calendar itself, and `dateFormat: 'Y-m-d'` keeps the value the input carries EXACTLY what the native
     * control produced, so nothing about what the API receives changes.
     */
    const enhanceDates = (root) => {
        const scope = root || global.document;
        if (!scope) { return 0; }

        const nodes = Array.from(scope.querySelectorAll('.flatpickr-date'))
            .filter((node) => typeof node.flatpickr === 'function' && !node._flatpickr);

        nodes.forEach((node) => {
            node.flatpickr({ monthSelectorType: 'static', dateFormat: 'Y-m-d', allowInput: true });
        });

        return nodes.length;
    };

    global.TaskForm = {
        DRAFT_STORAGE_KEY,
        TARGET,
        visibleFieldsFor,
        formatPositionLabel,
        formatPersonLabel,
        buildCreatePayload,
        buildUpdatePayload,
        validateDraft,
        readDraft,
        writeDraft,
        clearDraft,
        applyTargetVisibility,
        enhanceSelects,
        enhanceDates,
        enhanceTags,
        applyNotificationPreferences,
        WATCHER_ROLE,
        selectWatchers,
        renderPositionOptions,
        renderPersonOptions,
        // Phase 5 — configurable fields.
        customFieldControlKind,
        customFieldLabel,
        renderCustomFields,
        // Configurable fields backed by ANOTHER MODULE'S RECORDS.
        renderRecordOptions,
        readCustomFieldValues,
        writeCustomFieldValues,
        validateCustomFields
    };
})(typeof window !== 'undefined' ? window : globalThis);
