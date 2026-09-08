'use strict';

/*
 * MOD-0288-FU03 — Organization Unit custom field VALUES: render the tenant's definitions as controls, read them
 * back, validate them client-side, and render them read-only on the details page.
 *
 * ⚠ ADAPTED FROM `Tasks/form-page.js` + `Tasks/form.js`, DELIBERATELY NOT SHARED WITH THEM. FU02 §7 drew that
 * boundary for the backend and the reason is identical here: a shared screen module ties two modules' release
 * cycles together, so a Tasks change would land on the Organization form without anyone choosing it. The shapes
 * differ too — Tasks fields carry `valueType`/`optionsSourceKind`/`section`, organization fields carry
 * `dataType`/`validationRules`/`classification` — so "share it" would mean a translation layer on both sides.
 *
 * ⚠ THE SERVER DECIDES. Everything validated here exists to produce a better message a moment sooner. The
 * server refuses what it refuses, and its rejection wins; nothing here is an authorization or a data rule.
 *
 * ⚠ DEFINITIONS ARE AUTHORED IN FU04, NEVER HERE. This module reads whatever definitions exist and writes
 * values against them; it cannot create, change or deactivate one.
 */
(function (global) {
    const doc = global.document;

    // The FU02 data types (OrganizationFieldDataType). A type not in this map gets NO control and is not
    // rendered — an unrecognised type is a definition this build does not understand, and a free-text box for
    // it would produce values the server refuses with no way for the user to act on the refusal.
    const CONTROL_BY_TYPE = {
        Text: 'text',
        MultilineText: 'textarea',
        Integer: 'integer',
        Decimal: 'decimal',
        Boolean: 'boolean',
        Date: 'date',
        SingleSelect: 'select',
        Reference: 'reference'
    };

    const trim = (v) => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v).trim()));
    const controlId = (code) => `ouCustomField_${String(code).replace(/[^A-Za-z0-9_-]/g, '_')}`;

    /*
     * ⚠ THE CLASSIFICATION KEY IS DERIVED, NOT LISTED — mirroring OrganizationFieldMapper.ReadPermissionFor
     * exactly. A hard-coded list here would silently stop covering a classification the backend later adds,
     * and the failure mode of that omission is a confidential value rendered in the clear.
     */
    const classificationReadPermission = (classification) => {
        const value = trim(classification) || 'Normal';
        return value === 'Normal'
            ? null
            : `platform.organization-units.custom-fields.read.${value.toLowerCase()}`;
    };

    const hasPermission = (key) => {
        if (!key) return true;
        const snapshot = global.__permissionSnapshot;
        if (!Array.isArray(snapshot)) return false;
        const needle = String(key).toLowerCase();
        return snapshot.some((k) => String(k).toLowerCase() === needle);
    };

    /*
     * Which definitions this actor may see the VALUES of.
     *
     * ⚠ A FIELD THE ACTOR MAY NOT READ IS DROPPED, NOT BLANKED (§8, §10.8). A blank control still tells the
     * reader the datum exists and is unanswered — and on a form it invites a save that would overwrite a value
     * the user was never shown. Absent the grant, the field is not on the page at all.
     */
    const isReadable = (definition) =>
        !!definition && hasPermission(classificationReadPermission(definition.classification));

    const controlKindFor = (definition) => {
        if (!definition || definition.isActive === false) return null;
        const kind = CONTROL_BY_TYPE[trim(definition.dataType)] || null;
        if (kind === 'select') {
            // An empty dropdown is not a degraded control, it is an unusable one.
            const options = definition.validationRules && definition.validationRules.options;
            return Array.isArray(options) && options.length > 0 ? kind : null;
        }
        if (kind === 'reference') {
            // A Reference with no declared target has nothing to search; a raw GUID box is not a control.
            return trim(definition.validationRules && definition.validationRules.referenceTarget) ? kind : null;
        }
        return kind;
    };

    const applicableDefinitions = (definitions) =>
        (definitions || [])
            .filter((d) => d && d.isActive !== false && isReadable(d) && controlKindFor(d))
            .slice()
            .sort((a, b) =>
                (a.displayOrder || 0) - (b.displayOrder || 0)
                || String(a.name || a.code || '').localeCompare(String(b.name || b.code || '')));

    // ─── Control construction ────────────────────────────────────────────────────────────────────────────

    /*
     * The empty first option of a select.
     *
     * ⚠ IT MUST CARRY TEXT, AND NOT ONLY SO THE BOX READS WELL UNSTYLED. `form.js:initSelect2` takes each
     * select's own empty-option text AS THE SELECT2 PLACEHOLDER — that is the pattern this screen already
     * uses for Legal Entity, both parents and Manager Position. An empty string there produces an empty
     * placeholder, so the control looks blank whether or not select2 is bound. Found by the owner using the
     * screen: the custom-field selects were the only ones on the form with nothing in them.
     */
    const blankOption = (options) => {
        const blank = doc.createElement('option');
        blank.value = '';
        blank.textContent = options.selectPlaceholder || '';
        return blank;
    };

    /*
     * The hint a free-text control shows while empty.
     *
     * ⚠ DERIVED FROM THE TYPE, NEVER FROM THE DEFINITION. FU02 gives a definition no placeholder field and
     * adding one is out of scope, so this says what KIND of answer the box wants. Inventing a per-field hint
     * from the field's name would put words in the author's mouth that the author never wrote.
     */
    const placeholderFor = (kind, options) => {
        if (kind === 'integer' || kind === 'decimal') return options.numberPlaceholder || '';
        if (kind === 'date') return options.datePlaceholder || '';
        return options.textPlaceholder || '';
    };

    const buildControl = (definition, kind, options) => {
        const rules = definition.validationRules || {};
        let control;

        if (kind === 'textarea') {
            control = doc.createElement('textarea');
            control.className = 'form-control';
            control.rows = 3;
            control.placeholder = placeholderFor(kind, options);
            if (rules.maxLength) control.maxLength = rules.maxLength;
        } else if (kind === 'boolean') {
            control = doc.createElement('select');
            control.className = 'form-select';
            control.appendChild(blankOption(options));
            [['true', options.booleanYes || 'Yes'], ['false', options.booleanNo || 'No']]
                .forEach(([value, text]) => {
                    const opt = doc.createElement('option');
                    opt.value = value;
                    opt.textContent = text;
                    control.appendChild(opt);
                });
        } else if (kind === 'select') {
            control = doc.createElement('select');
            control.className = 'form-select';
            control.appendChild(blankOption(options));
            (rules.options || []).forEach((value) => {
                const opt = doc.createElement('option');
                opt.value = value;
                opt.textContent = value;
                control.appendChild(opt);
            });
        } else if (kind === 'reference') {
            control = doc.createElement('select');
            control.className = 'form-select';
            control.appendChild(blankOption(options));
            const target = trim(rules.referenceTarget);
            const rows = (options.references && options.references[target]) || [];
            rows.forEach((row) => {
                const opt = doc.createElement('option');
                opt.value = row.value;
                opt.textContent = row.text;
                control.appendChild(opt);
            });
        } else {
            control = doc.createElement('input');
            control.className = 'form-control';
            control.autocomplete = 'off';
            control.placeholder = placeholderFor(kind, options);
            if (kind === 'date') {
                control.type = 'date';
            } else if (kind === 'integer' || kind === 'decimal') {
                control.type = 'number';
                if (kind === 'integer') control.step = '1';
                if (rules.minValue != null) control.min = String(rules.minValue);
                if (rules.maxValue != null) control.max = String(rules.maxValue);
            } else {
                control.type = 'text';
                if (rules.maxLength) control.maxLength = rules.maxLength;
            }
        }

        control.id = controlId(definition.code);
        control.setAttribute('data-ou-custom-field', definition.code);
        control.setAttribute('data-ou-custom-field-type', trim(definition.dataType));
        return control;
    };

    /*
     * Render into `container` and return the definitions that ACTUALLY produced a control, so the caller can
     * decide whether the section has anything to show. A tenant with no definitions — or with only definitions
     * this actor may not read — must see no section at all, not an empty heading (§10.6).
     */
    const renderCustomFields = (container, definitions, options) => {
        if (!container) return [];
        const opts = options || {};
        container.innerHTML = '';
        const rendered = [];

        applicableDefinitions(definitions).forEach((definition) => {
            const kind = controlKindFor(definition);
            const column = doc.createElement('div');
            column.className = 'col-md-6';

            const label = doc.createElement('label');
            label.className = 'form-label fw-medium';
            label.textContent = definition.name || definition.code || '';
            label.setAttribute('for', controlId(definition.code));
            if (definition.isRequired) {
                const mark = doc.createElement('span');
                mark.className = 'text-danger';
                mark.textContent = ' *';
                label.appendChild(mark);
            }

            const control = buildControl(definition, kind, opts);
            // ⚠ Read-only, not hidden: an actor who may read a value but not write it should still SEE it.
            if (opts.readOnly) control.disabled = true;

            const feedback = doc.createElement('div');
            feedback.className = 'invalid-feedback';
            feedback.setAttribute('data-ou-custom-field-error', definition.code);

            column.appendChild(label);
            column.appendChild(control);
            column.appendChild(feedback);
            container.appendChild(column);
            rendered.push(definition);
        });

        enhanceSelects(container);
        return rendered;
    };

    /*
     * Bind select2 to the selects this module just created.
     *
     * ⚠ IT HAS TO HAPPEN HERE, NOT IN `form.js`. That file's `initSelect2` walks a FIXED ID LIST
     * (#ouLegalEntityId, #ouParentId, #ouAdministrativeParentId, #ouManagerPositionId) and runs while the page
     * is loading — custom fields have dynamic ids and do not exist in the DOM until the definitions have been
     * fetched. Widening that list would not help; it would still run before these controls are born.
     *
     * ⚠ AND IT IS THE SAME BINDING, NOT A SECOND STYLE: width '100%', and the select's own empty-option text
     * as the placeholder — copied from `form.js:initSelect2` so the custom fields sit in a form that looks
     * like one form.
     */
    const enhanceSelects = (container) => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2 || !container) return;
        jq(container).find('select[data-ou-custom-field]').each(function () {
            const $select = jq(this);
            // select2's own marker for "already bound" — re-rendering the section must not stack instances.
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            const placeholder = $select.find('option[value=""]').first().text() || '';
            $select.select2({ width: '100%', placeholder });
        });
    };

    /*
     * Write a value into a control that MAY be select2-bound.
     *
     * ⚠ A BARE `control.value = x` IS INVISIBLE ONCE SELECT2 OWNS THE SELECT. select2 renders its own element
     * and only redraws on a jQuery `change`; without it, binding select2 (which this module now does) would
     * have made every stored custom value vanish from the edit form while still sitting in the underlying
     * select. That is the same class of silent wrong-state the reporting-line fallback was — the screen shows
     * empty and the next save writes empty.
     */
    const setControlValue = (control, value) => {
        control.value = value;
        const jq = window.jQuery;
        if (jq?.fn?.select2 && control.tagName === 'SELECT' && control.classList.contains('select2-hidden-accessible')) {
            jq(control).val(value).trigger('change');
        }
    };

    const controlFor = (container, code) =>
        container ? container.querySelector(`[data-ou-custom-field="${code}"]`) : null;

    /*
     * Write stored values into the rendered controls.
     *
     * ⚠ A ROW FLAGGED `redacted` CARRIES NO VALUE AND MUST NOT BE TREATED AS EMPTY. The server returns the row
     * so the caller knows the datum exists, with the value omitted — writing "" into the control would turn
     * "you may not read this" into "this is blank", and the next save would clear a value the user never saw.
     * Such definitions are already filtered out by isReadable; this is the second line of the same defence.
     */
    const writeCustomFieldValues = (container, definitions, values) => {
        const byDefinition = {};
        (values || []).forEach((v) => {
            const id = v.definitionId || v.DefinitionId;
            if (id) byDefinition[String(id).toLowerCase()] = v;
        });

        (definitions || []).forEach((definition) => {
            const control = controlFor(container, definition.code);
            if (!control) return;
            const stored = byDefinition[String(definition.id).toLowerCase()];
            if (!stored || stored.redacted === true || stored.Redacted === true) return;
            const raw = stored.value ?? stored.Value;
            if (raw == null) return;
            let value = String(raw);
            if (trim(definition.dataType) === 'Date' && value.length >= 10) value = value.slice(0, 10);
            if (trim(definition.dataType) === 'Boolean') value = String(value).toLowerCase();
            setControlValue(control, value);
        });
    };

    /*
     * Read the controls back as [{ definitionId, code, value }]. An empty control yields `value: ''`, which is
     * the contract's CLEAR — the caller compares against what was loaded and sends only what actually changed,
     * so an untouched empty optional field produces no write at all.
     */
    const readCustomFieldValues = (container, definitions) =>
        (definitions || []).map((definition) => {
            const control = controlFor(container, definition.code);
            if (!control) return null;
            return { definitionId: definition.id, code: definition.code, value: trim(control.value) };
        }).filter(Boolean);

    const isWellFormed = (dataType, value) => {
        switch (dataType) {
            case 'Integer': return /^-?\d+$/.test(value);
            case 'Decimal': return /^-?\d+([.,]\d+)?$/.test(value);
            case 'Boolean': return value === 'true' || value === 'false';
            case 'Date': return !Number.isNaN(new Date(value).getTime());
            default: return true;
        }
    };

    /*
     * Client validation: required-and-empty, and type well-formedness. Returns { valid, errors:[{code,message}] }.
     * Constraint ranges and lengths are left to the native input attributes and to the server — inventing a
     * message for each of them would mean inventing strings the owner never approved (§7).
     */
    const validateCustomFields = (definitions, values, labels) => {
        const L = labels || {};
        const byCode = {};
        (values || []).forEach((v) => { byCode[v.code] = v.value; });
        const errors = [];

        (definitions || []).forEach((definition) => {
            const value = trim(byCode[definition.code]);
            if (!value.length) {
                if (definition.isRequired) {
                    errors.push({ code: definition.code, message: L.CustomFieldRequiredError || 'This field is required' });
                }
                return;
            }
            if (!isWellFormed(trim(definition.dataType), value)) {
                errors.push({ code: definition.code, message: L.CustomFieldTypeError || 'The value does not match the field type' });
            }
        });

        return { valid: errors.length === 0, errors };
    };

    const showCustomFieldErrors = (container, errors) => {
        if (!container) return;
        container.querySelectorAll('[data-ou-custom-field]').forEach((el) => el.classList.remove('is-invalid'));
        container.querySelectorAll('[data-ou-custom-field-error]').forEach((el) => { el.textContent = ''; });
        (errors || []).forEach(({ code, message }) => {
            controlFor(container, code)?.classList.add('is-invalid');
            const feedback = container.querySelector(`[data-ou-custom-field-error="${code}"]`);
            if (feedback) feedback.textContent = message;
        });
    };

    /*
     * Read-only rendering for the details page. Returns how many fields were rendered so the caller can keep the
     * section hidden when the answer is zero. A redacted row is SKIPPED — hidden, never blank (§10.8).
     */
    const renderCustomFieldValues = (container, definitions, values, options) => {
        if (!container) return 0;
        const opts = options || {};
        container.innerHTML = '';
        const byDefinition = {};
        (values || []).forEach((v) => {
            const id = v.definitionId || v.DefinitionId;
            if (id) byDefinition[String(id).toLowerCase()] = v;
        });

        let count = 0;
        applicableDefinitions(definitions).forEach((definition) => {
            const stored = byDefinition[String(definition.id).toLowerCase()];
            if (stored && (stored.redacted === true || stored.Redacted === true)) return;

            const raw = stored ? (stored.value ?? stored.Value) : null;
            let text = raw == null || String(raw).length === 0 ? '-' : String(raw);
            if (text !== '-' && trim(definition.dataType) === 'Boolean') {
                text = text.toLowerCase() === 'true' ? (opts.booleanYes || 'Yes') : (opts.booleanNo || 'No');
            }

            const column = doc.createElement('div');
            column.className = 'col-md-6';
            const field = doc.createElement('div');
            field.className = 'backbone-preview-field';
            const icon = doc.createElement('i');
            icon.className = 'bx bx-detail';
            const body = doc.createElement('div');
            const label = doc.createElement('div');
            label.className = 'backbone-preview-label';
            label.textContent = definition.name || definition.code || '';
            const value = doc.createElement('div');
            value.className = 'backbone-preview-value mt-1';
            value.textContent = text;
            body.appendChild(label);
            body.appendChild(value);
            field.appendChild(icon);
            field.appendChild(body);
            column.appendChild(field);
            container.appendChild(column);
            count += 1;
        });

        return count;
    };

    global.OrgUnitCustomFields = {
        classificationReadPermission,
        hasPermission,
        applicableDefinitions,
        renderCustomFields,
        writeCustomFieldValues,
        readCustomFieldValues,
        validateCustomFields,
        showCustomFieldErrors,
        renderCustomFieldValues
    };
})(window);
