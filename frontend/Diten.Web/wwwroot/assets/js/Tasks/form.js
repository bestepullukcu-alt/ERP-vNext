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
            if (kind === 'record') {
                select.setAttribute('data-custom-field-record', '1');
                /*
                 * A record picker gets select2's LOOK but not select2's search box, and the reason is that the two
                 * searches are not the same search. select2 filters the <option>s already in the DOM; a record
                 * control holds ONE PAGE of a source that can have thousands of rows. Offering select2's box here
                 * would search the page while looking like it searched the source — "no results" for a record that
                 * exists. The search that reaches the server is the input rendered beside the control, and it stays
                 * the only one, so the user sees one search box rather than two that disagree.
                 */
                select.setAttribute('data-select2-search', 'off');
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
            column.className = 'col-md-4';

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
             * A record field gets a SEARCH BOX, and this is the whole reason it is not a plain dropdown: a source
             * can hold five thousand records, and an <option> list of five thousand is not a control — it is a
             * scroll bar with a truncation nobody announced. The user types, the SERVER searches, the page comes
             * back.
             */
            if (kind === 'record') {
                const search = doc.createElement('input');
                search.type = 'search';
                search.className = 'form-control form-control-sm mb-2';
                search.setAttribute('data-custom-field-search', definition.code);
                search.setAttribute('aria-label', `${label.textContent}`);
                search.placeholder = (labels && labels.recordSearchPlaceholder) || '';
                column.appendChild(search);
            }

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
    const enhanceSelects = (root) => {
        const scope = root || global.document;
        if (!scope || typeof global.jQuery !== 'function') { return 0; }

        const nodes = Array.from(scope.querySelectorAll('select.select2'))
            .filter((node) => !node.classList.contains('select2-hidden-accessible'));

        nodes.forEach((node) => {
            const $node = global.jQuery(node);
            $node.wrap('<div class="position-relative"></div>');
            const settings = { dropdownParent: $node.parent() };
            // See buildCustomFieldControl: a server-searched control must not also offer select2's own box.
            if (node.getAttribute('data-select2-search') === 'off') {
                settings.minimumResultsForSearch = Infinity;
            }
            $node.select2(settings);
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
        validateDraft,
        readDraft,
        writeDraft,
        clearDraft,
        applyTargetVisibility,
        enhanceSelects,
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
