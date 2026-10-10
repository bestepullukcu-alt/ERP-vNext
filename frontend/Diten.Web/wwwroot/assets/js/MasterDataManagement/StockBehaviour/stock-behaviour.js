/**
 * MOD-0290-FU04 S5 (O-12) — the "Stock behaviour" section of the GSKU / Finished Good / LSKU quick view and its own
 * edit offcanvas. One module for the three screens; each page creates it with its level, its proxy and its detail read.
 *
 *  - The section and the offcanvas are in the page only for a user with the level's stock-edit key (UAS-001, decided in
 *    the cshtml). Without the offcanvas `create` answers null — no section, no shortcut, nothing to open.
 *  - Values are drawn from the detail's `stockBehaviour` (MDM's StockBehaviourDto) with their source ("from GSKU"),
 *    stockable yes / no and — when not — the missing fields by name, and the lock markers (O-8).
 *  - O-8 in the form: on a Draft every field is free; on an approved record a field in `lockedFields` is read-only and
 *    says why, and a list in `addOnlyFields` keeps its stored rows read-only and only gains new ones (sent whole).
 *  - Only changed fields are posted, with the version the form was opened at and the form attempt's id (the proxy
 *    derives the Idempotency-Key from it: a repeat of the same attempt is MDM's replay). The detail is read back after.
 *  - S5b (BL-600): units and storage conditions are SELECTS filled from the item options read (Items proxy
 *    api/options), labelled by stable code (an unknown code is shown as itself). A list that cannot be loaded leaves
 *    those fields passive with the reason beside them — never a typed-code box; the other fields stay usable. At most
 *    one temperature storage condition (O-2, the list's ConditionKind). MDM still validates every chosen code and its
 *    refusal comes back as a sentence in the reader's language.
 *  - A Finished Good saved in two requests (stock, then GTIN) whose GTIN request fails is still read back: the stock
 *    part is MDM's new version, the editor moves to it, and the GTIN refusal is said in its own sentence (S5-FIX1).
 */
'use strict';

window.DitenStockBehaviour = (function () {
    // The fields each level edits here, in the order the proxy reads them (pack §4.2; the LSKU only its serial flag, O-3).
    const levels = Object.freeze({
        gsku: ['baseUomCode', 'uomConversions', 'shelfLifeDays', 'retestDays', 'minRemainingShelfLifeDays',
            'storageConditionCodes', 'lotControlled', 'stockable'],
        fg: ['uomConversions', 'shelfLifeDays', 'storageConditionCodes', 'lotControlled', 'serialControlled', 'stockable',
            'gtin'],
        lsku: ['serialControlled']
    });
    const formNames = Object.freeze({
        baseUomCode: 'BaseUomCode', uomConversions: 'UomConversions', shelfLifeDays: 'ShelfLifeDays',
        retestDays: 'RetestDays', minRemainingShelfLifeDays: 'MinRemainingShelfLifeDays',
        storageConditionCodes: 'StorageConditionCodes', lotControlled: 'LotControlled',
        serialControlled: 'SerialControlled', stockable: 'Stockable', gtin: 'Gtin'
    });
    const dayFields = new Set(['shelfLifeDays', 'retestDays', 'minRemainingShelfLifeDays']);
    const flagFields = new Set(['lotControlled', 'serialControlled', 'stockable']);
    // Which source field says a value is the GSKU's (O-3 / D-2 / O-14).
    const sourceFields = Object.freeze({
        baseUomCode: 'baseUomSource', shelfLifeDays: 'shelfLifeDaysSource', retestDays: 'daysSource',
        minRemainingShelfLifeDays: 'daysSource', lotControlled: 'lotControlledSource'
    });
    // MDM's `missing` names → their words. A name not here is never shown raw.
    const missingWords = Object.freeze({
        baseUomCode: 'BaseUom', shelfLifeDays: 'ShelfLife', storageConditionCodes: 'StorageConditions',
        lotControlled: 'LotControlled', serialControlled: 'SerialControlled', gtin: 'Gtin', stockable: 'StockableFlag',
        caseBoxConversion: 'MissingCaseBox', 'gsku.baseUomCode': 'MissingGskuBaseUom', 'gsku.stockable': 'MissingGskuStockable'
    });
    // The lists the stock forms choose from (S5b): one read per page, kept once it succeeded; a failure is asked again
    // at the next opening. The Items proxy answers any of the stock-edit keys (ItemsController.OptionsPermissions).
    const optionsEndpoint = '/MasterDataManagement/Items/api/options';
    const listFields = Object.freeze({ baseUomCode: 'uoms', uomConversions: 'uoms', storageConditionCodes: 'storageConditions' });
    const temperatureKind = 'TEMPERATURE';
    let optionsRead = null;
    const codePattern = /^[A-Z][A-Z0-9_]{0,63}$/;
    const amountPattern = /^[0-9]{1,18}(\.[0-9]{1,18})?$/;
    const maxDays = 3650;

    const valueOf = (source, camel) => source?.[camel] ?? source?.[camel.charAt(0).toUpperCase() + camel.slice(1)];
    const listOf = (value) => Array.isArray(value) ? value : [];
    const readWords = () => {
        try {
            const raw = JSON.parse(document.getElementById('stock-behaviour-l10n')?.textContent || '{}');
            return Object.fromEntries(Object.entries(raw).map(([key, text]) => [key.charAt(0).toUpperCase() + key.slice(1), text]));
        } catch (error) {
            return {};
        }
    };
    // One id per form attempt (opening the offcanvas); a repeat of the same attempt sends the same id.
    const newAttemptId = () => {
        if (typeof crypto?.randomUUID === 'function') return crypto.randomUUID();
        const bytes = crypto.getRandomValues(new Uint8Array(16));
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;
        const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
        return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
    };
    const sameList = (left, right) => JSON.stringify(left) === JSON.stringify(right);
    const readOptionList = (data, name) => listOf(valueOf(data, name))
        .filter((option) => typeof valueOf(option, 'code') === 'string' && codePattern.test(valueOf(option, 'code')))
        .map((option) => ({ code: valueOf(option, 'code'), attributes: valueOf(option, 'attributes') || {} }));
    // { ready, lists, reason }: ready only with both lists non-empty — an empty list is said as unavailable.
    const unavailable = (reason) => ({ ready: false, lists: { uoms: [], storageConditions: [] }, reason });
    const loadOptions = (fallback) => {
        if (optionsRead) return optionsRead;
        const attempt = (async () => {
            try {
                const response = await fetch(optionsEndpoint, {
                    credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                let payload = {};
                try { payload = await response.json(); } catch (error) { }
                if (!response.ok) {
                    const errors = payload?.errors || payload?.Errors;
                    return unavailable((Array.isArray(errors) && errors.find(Boolean)) || fallback);
                }
                const data = payload?.data ?? payload?.Data;
                const lists = { uoms: readOptionList(data, 'uoms'), storageConditions: readOptionList(data, 'storageConditions') };
                if (!lists.uoms.length || !lists.storageConditions.length) return unavailable(fallback);
                return { ready: true, lists, reason: '' };
            } catch (error) {
                return unavailable(fallback);
            }
        })();
        optionsRead = attempt.then((result) => {
            if (!result.ready) optionsRead = null;
            return result;
        });
        return optionsRead;
    };
    const conversionOf = (row) => ({
        fromUom: String(valueOf(row, 'fromUom') ?? ''), toUom: String(valueOf(row, 'toUom') ?? ''),
        numerator: String(valueOf(row, 'numerator') ?? ''), denominator: String(valueOf(row, 'denominator') ?? '')
    });
    // S5c: the units by name with the code beside them (unitShown); the code alone where the vocabulary has no name.
    const conversionText = (row, unitShown = (code) => code) => {
        const item = conversionOf(row);
        const amount = item.denominator === '1' ? item.numerator : `${item.numerator}/${item.denominator}`;
        return `1 ${unitShown(item.fromUom)} = ${amount} ${unitShown(item.toUom)}`;
    };

    const create = ({ level, endpoint, versionField, fetchDetail, onSaved } = {}) => {
        const offcanvas = document.getElementById('offcanvasStockBehaviour');
        if (!levels[level] || !offcanvas || typeof fetchDetail !== 'function') return null;
        const words = readWords();
        const fields = levels[level];
        const needsOptions = fields.some((field) => listFields[field]);
        let editor = null;
        let busy = false;
        let options = { ready: false, lists: { uoms: [], storageConditions: [] }, reason: '' };

        const toast = (message, type) => window.showToast?.(message, type);
        const yesNo = (value) => value === true ? words.Yes : value === false ? words.No : words.NotSet;
        const shown = (value) => value === null || value === undefined || value === '' ? words.NotSet : String(value);
        const uomLabel = (code) => (code && words[`Uom_${code}`]) || code;
        const storageLabel = (code) => (code && words[`Storage_${code}`]) || code;
        const labels = { uoms: uomLabel, storageConditions: storageLabel };
        // S5c: a stored value by its name with its code ("Kilogram (KGM)"); a newer catalog value without a name by its code.
        const named = (label, code) => (code ? (label && label !== code ? `${label} (${code})` : code) : code);
        const uomShown = (code) => named(uomLabel(code), code);
        const storageShown = (code) => named(storageLabel(code), code);
        // What MDM holds as THIS record's own value (the form compares against it; an inherited one is not its own).
        const stored = (sb, field) => {
            if (field === 'stockable') return valueOf(sb, 'stockableFlag') ?? null;
            if (field === 'shelfLifeDays' && level === 'fg') {
                return valueOf(sb, 'shelfLifeDaysSource') === 'Own' ? valueOf(sb, 'shelfLifeDays') ?? null : null;
            }
            if (field === 'uomConversions') return listOf(valueOf(sb, 'uomConversions')).map(conversionOf);
            if (field === 'storageConditionCodes') return listOf(valueOf(sb, 'storageConditionCodes')).map(String);
            return valueOf(sb, field) ?? null;
        };

        /* ---- the section (quick view) ---- */
        const render = (detail) => {
            const section = document.getElementById('sb-section');
            if (!section) return;
            const sb = valueOf(detail, 'stockBehaviour');
            section.classList.toggle('d-none', !sb);
            if (!sb) return;
            const set = (field, text) => {
                const element = section.querySelector(`[data-sb-value="${field}"]`);
                if (element) element.textContent = text;
            };
            set('baseUomCode', shown(uomShown(valueOf(sb, 'baseUomCode'))));
            set('shelfLifeDays', shown(valueOf(sb, 'shelfLifeDays')));
            set('retestDays', shown(valueOf(sb, 'retestDays')));
            set('minRemainingShelfLifeDays', shown(valueOf(sb, 'minRemainingShelfLifeDays')));
            const storage = listOf(valueOf(sb, 'storageConditionCodes'));
            set('storageConditionCodes', storage.length ? storage.map(storageShown).join(', ') : words.NotSet);
            set('lotControlled', yesNo(valueOf(sb, 'lotControlled')));
            set('serialControlled', yesNo(valueOf(sb, 'serialControlled')));
            set('gtin', shown(valueOf(sb, 'gtin')));
            set('stockable', yesNo(valueOf(sb, 'stockable') === true));
            const conversions = section.querySelector('[data-sb-value="uomConversions"]');
            if (conversions) {
                const lines = listOf(valueOf(sb, 'uomConversions')).map((row) => conversionText(row, uomShown));
                const derived = valueOf(sb, 'derivedPackConversion');
                if (derived) lines.push(`${conversionText(derived, uomShown)} — ${words.DerivedPackConversion || ''}`);
                conversions.replaceChildren(...(lines.length ? lines : [words.NotSet]).map((line) => {
                    const row = document.createElement('div');
                    row.textContent = line;
                    return row;
                }));
            }
            section.querySelectorAll('[data-sb-source]').forEach((badge) => badge.classList.toggle('d-none',
                valueOf(sb, sourceFields[badge.dataset.sbSource] || '') !== 'Gsku'));
            const locked = listOf(valueOf(sb, 'lockedFields'));
            section.querySelectorAll('[data-sb-lock]').forEach((marker) =>
                marker.classList.toggle('d-none', !locked.includes(marker.dataset.sbLock)));
            const missing = listOf(valueOf(sb, 'missing')).map((name) => words[missingWords[name]]).filter(Boolean);
            const notStockable = valueOf(sb, 'stockable') !== true;
            section.querySelector('[data-sb-missing-row]')?.classList.toggle('d-none', !notStockable);
            section.querySelector('[data-sb-missing]')?.replaceChildren(...missing.map((text) => {
                const badge = document.createElement('span');
                badge.className = 'badge bg-label-warning';
                badge.textContent = text;
                return badge;
            }));
            const edit = document.getElementById('sb-edit');
            if (edit) {
                edit.dataset.id = String(valueOf(detail, 'id') ?? '');
                edit.classList.toggle('d-none', valueOf(sb, 'editable') !== true);
            }
        };

        /* ---- the form (offcanvas) ---- */
        const input = (field) => offcanvas.querySelector(`[data-sb-input="${field}"]`);
        // select2 when the page has it (the shell's picker), a plain select otherwise; select2 announces a choice with a
        // jQuery change event only, so a change is heard through jQuery when it is there.
        const select2Available = () => typeof window.jQuery === 'function' && typeof window.jQuery.fn?.select2 === 'function';
        const enhanceSelect = (select) => {
            if (!select || !select2Available()) return;
            const $select = window.jQuery(select);
            if ($select.hasClass('select2-hidden-accessible')) return;
            $select.select2({
                width: '100%', dropdownParent: window.jQuery(offcanvas), allowClear: false,
                placeholder: select.dataset.placeholder || '', closeOnSelect: !select.multiple
            });
        };
        const refreshSelect = (select) => {
            if (select && select2Available()) window.jQuery(select).trigger('change.select2');
        };
        const selectedCodes = (select) => Array.from(select?.selectedOptions || [], (option) => option.value).filter(Boolean);
        // The list's codes in its order (minus `except`); a chosen code the list no longer offers stays, so a stored
        // value is shown and never silently dropped.
        const fillSelect = (select, listName, chosen, except = []) => {
            if (!select) return;
            const label = labels[listName];
            const keep = new Set(chosen.filter(Boolean));
            const codes = options.lists[listName].map((option) => option.code).filter((code) => !except.includes(code));
            const placeholder = select.multiple ? [] : [Object.assign(document.createElement('option'), {
                value: '', textContent: words.SelectUnit || '', selected: keep.size === 0
            })];
            select.replaceChildren(...placeholder, ...[...codes, ...[...keep].filter((code) => !codes.includes(code))]
                .map((code) => Object.assign(document.createElement('option'), {
                    value: code, textContent: label(code), selected: keep.has(code)
                })));
            refreshSelect(select);
        };
        const temperatureCodes = () => new Set(options.lists.storageConditions
            .filter((option) => valueOf(option.attributes, 'conditionKind') === temperatureKind)
            .map((option) => option.code));
        // O-2: at most one TEMPERATURE condition, counting the stored rows of an add-only list. A second one is refused
        // in words and taken back (the one chosen before stays); the other temperatures are passive while one is held.
        const enforceOneTemperature = () => {
            const select = input('storageConditionCodes');
            if (!select || !editor || !options.ready) return;
            const temperatures = temperatureCodes();
            const addOnly = listOf(valueOf(editor.sb, 'addOnlyFields')).includes('storageConditionCodes');
            const fixed = addOnly ? stored(editor.sb, 'storageConditionCodes').filter((code) => temperatures.has(code)) : [];
            const chosen = selectedCodes(select).filter((code) => temperatures.has(code));
            const refused = fixed.length + chosen.length > 1;
            if (refused) {
                const keep = fixed[0] || chosen.find((code) => editor.storage.includes(code)) || chosen[0];
                Array.from(select.options).forEach((option) => {
                    if (temperatures.has(option.value) && option.value !== keep) option.selected = false;
                });
            }
            const note = offcanvas.querySelector('#sb-storage-temperature-note');
            if (note) {
                note.textContent = refused ? words.TemperatureOnlyOne || '' : '';
                note.classList.toggle('d-none', !refused);
            }
            const held = fixed[0] || selectedCodes(select).find((code) => temperatures.has(code));
            Array.from(select.options).forEach((option) => {
                if (temperatures.has(option.value)) option.disabled = Boolean(held) && option.value !== held;
            });
            editor.storage = selectedCodes(select);
            refreshSelect(select);
        };
        const conversionRow = (row, readOnly) => {
            const line = document.createElement('div');
            line.className = 'row g-2 align-items-center';
            line.dataset.sbConversionRow = readOnly ? 'stored' : 'open';
            const parts = [['fromUom', words.ConversionFrom, 'col-3'], ['toUom', words.ConversionTo, 'col-3'],
                ['numerator', words.ConversionNumerator, 'col-2'], ['denominator', words.ConversionDenominator, 'col-2']];
            parts.forEach(([part, label, column]) => {
                const cell = document.createElement('div');
                cell.className = column;
                const unit = part.endsWith('Uom');
                // A unit is chosen from the list (S5b) — never typed; without the list it stays passive.
                const box = document.createElement(unit ? 'select' : 'input');
                box.dataset.sbPart = part;
                box.setAttribute('aria-label', label || part);
                if (unit) {
                    box.className = 'form-select form-select-sm';
                    fillSelect(box, 'uoms', row ? [conversionOf(row)[part]] : []);
                } else {
                    box.type = 'text';
                    box.className = 'form-control form-control-sm';
                    box.value = row ? conversionOf(row)[part] : '';
                    box.placeholder = label || '';
                    box.inputMode = 'decimal';
                    box.readOnly = readOnly;
                }
                box.disabled = readOnly || (unit && !options.ready);
                cell.appendChild(box);
                line.appendChild(cell);
            });
            const action = document.createElement('div');
            action.className = 'col-2 text-end';
            if (!readOnly) {
                const remove = document.createElement('button');
                remove.type = 'button';
                remove.className = 'btn btn-sm btn-icon btn-label-danger';
                remove.dataset.sbRemove = 'true';
                remove.setAttribute('aria-label', words.RemoveConversion || '');
                remove.title = words.RemoveConversion || '';
                const icon = document.createElement('i');
                icon.className = 'bx bx-trash';
                remove.appendChild(icon);
                action.appendChild(remove);
            }
            line.appendChild(action);
            return line;
        };
        const fill = (sb) => {
            const editable = valueOf(sb, 'editable') === true;
            const locked = new Set(listOf(valueOf(sb, 'lockedFields')));
            const addOnly = new Set(listOf(valueOf(sb, 'addOnlyFields')));
            offcanvas.querySelector('#sb-form-not-editable')?.classList.toggle('d-none', editable);
            offcanvas.querySelectorAll('[data-sb-options-reason]').forEach((reason) => {
                reason.textContent = options.ready ? '' : options.reason || words.OptionsUnavailable || '';
                reason.classList.toggle('d-none', options.ready);
            });
            fields.forEach((field) => {
                const element = input(field);
                if (!element) return;
                const lockedHere = locked.has(field);
                offcanvas.querySelector(`[data-sb-lock-reason="${field}"]`)?.classList.toggle('d-none', !lockedHere);
                offcanvas.querySelector(`[data-sb-addonly-reason="${field}"]`)?.classList.toggle('d-none', !addOnly.has(field));
                const value = stored(sb, field);
                if (field === 'uomConversions') {
                    element.replaceChildren(...value.map((row) => conversionRow(row, addOnly.has(field) || !editable)));
                    const add = offcanvas.querySelector('#sb-add-conversion');
                    if (add) add.disabled = !editable || !options.ready;
                    return;
                }
                if (field === 'storageConditionCodes') {
                    // Add-only: the stored codes stand as read-only badges; the box takes only new ones.
                    const existing = offcanvas.querySelector('[data-sb-existing="storageConditionCodes"]');
                    existing?.replaceChildren(...(addOnly.has(field) ? value : []).map((code) => {
                        const badge = document.createElement('span');
                        badge.className = 'badge bg-label-secondary';
                        badge.textContent = storageLabel(code);
                        return badge;
                    }));
                    // Add-only: the select offers only what is not stored yet; otherwise it holds the stored choice.
                    fillSelect(element, 'storageConditions', addOnly.has(field) ? [] : value, addOnly.has(field) ? value : []);
                } else if (field === 'baseUomCode') {
                    fillSelect(element, 'uoms', [value === null ? '' : String(value)]);
                } else if (flagFields.has(field)) {
                    element.value = value === true ? 'true' : value === false ? 'false' : '';
                    // "Not set" is offered only while nothing is stored — a flag is never cleared (MDM has no "unset").
                    const unset = element.querySelector('option[value=""]');
                    if (unset) {
                        unset.disabled = value !== null;
                        unset.hidden = value !== null;
                    }
                } else {
                    element.value = value === null ? '' : String(value);
                }
                if (field === 'shelfLifeDays' && level === 'fg') {
                    element.placeholder = valueOf(sb, 'shelfLifeDaysSource') === 'Gsku'
                        ? String(valueOf(sb, 'shelfLifeDays') ?? '') : '';
                }
                element.disabled = !editable || lockedHere || (Boolean(listFields[field]) && !options.ready);
                element.readOnly = !editable || lockedHere;
                if (listFields[field]) enhanceSelect(element);
            });
            if (editor) editor.storage = selectedCodes(input('storageConditionCodes'));
            enforceOneTemperature();
            const derived = offcanvas.querySelector('#sb-form-derived');
            if (derived) {
                const edge = valueOf(sb, 'derivedPackConversion');
                derived.textContent = edge ? `${conversionText(edge, uomShown)} — ${words.DerivedPackConversion || ''}` : '';
                derived.classList.toggle('d-none', !edge);
            }
            const save = offcanvas.querySelector('#btnSaveStockBehaviour');
            if (save) save.disabled = !editable;
        };

        // The changed fields only, shape-checked; { stock, gtin, error }. A field locked (O-8) or a record that may not
        // be edited sends nothing.
        const collect = () => {
            const sb = editor.sb;
            const editable = valueOf(sb, 'editable') === true;
            const locked = new Set(listOf(valueOf(sb, 'lockedFields')));
            const addOnly = new Set(listOf(valueOf(sb, 'addOnlyFields')));
            const stock = {};
            let gtin = null;
            if (!editable) return { stock, gtin, error: '' };
            for (const field of fields) {
                const element = input(field);
                // A list field without its list is passive (S5b): nothing of it is sent.
                if (!element || locked.has(field) || (listFields[field] && !options.ready)) continue;
                const before = stored(sb, field);
                if (field === 'uomConversions') {
                    const rows = [...element.querySelectorAll('[data-sb-conversion-row]')].map((line) => {
                        const part = (name) => String(line.querySelector(`[data-sb-part="${name}"]`)?.value || '').trim();
                        return { fromUom: part('fromUom').toUpperCase(), toUom: part('toUom').toUpperCase(),
                            numerator: part('numerator'), denominator: part('denominator') };
                    }).filter((row) => row.fromUom || row.toUom || row.numerator || row.denominator);
                    if (rows.some((row) => !codePattern.test(row.fromUom) || !codePattern.test(row.toUom)
                        || row.fromUom === row.toUom || !amountPattern.test(row.numerator) || !amountPattern.test(row.denominator)
                        || Number(row.numerator) <= 0 || Number(row.denominator) <= 0)) {
                        return { error: words.InvalidConversion };
                    }
                    // Add-only: the stored rows are drawn read-only and travel with the new ones (the list is sent whole).
                    if (!rows.length && before.length) return { error: words.ListCannotBeEmpty };
                    if (!sameList(rows, before) && rows.length) stock[field] = rows;
                    continue;
                }
                const raw = String(element.value || '').trim();
                if (field === 'storageConditionCodes') {
                    const typed = selectedCodes(element);
                    if (typed.some((code) => !codePattern.test(code))) return { error: words.InvalidCode };
                    const codes = [...new Set(addOnly.has(field) ? [...before, ...typed] : typed)];
                    const temperatures = temperatureCodes();
                    if (codes.filter((code) => temperatures.has(code)).length > 1) return { error: words.TemperatureOnlyOne };
                    if (!codes.length && before.length) return { error: words.ListCannotBeEmpty };
                    if (codes.length && !sameList(codes, before)) stock[field] = codes;
                    continue;
                }
                if (raw === '') continue;
                if (field === 'gtin') {
                    if (!/^[0-9]{14}$/.test(raw)) return { error: words.InvalidGtin };
                    if (raw !== before) gtin = raw;
                } else if (field === 'baseUomCode') {
                    const code = raw.toUpperCase();
                    if (!codePattern.test(code)) return { error: words.InvalidCode };
                    if (code !== before) stock[field] = code;
                } else if (dayFields.has(field)) {
                    if (!/^[0-9]{1,4}$/.test(raw) || Number(raw) > maxDays) return { error: words.InvalidDays };
                    if (Number(raw) !== before) stock[field] = Number(raw);
                } else if (flagFields.has(field)) {
                    const flag = raw === 'true';
                    if (flag !== before) stock[field] = flag;
                }
            }
            return { stock, gtin, error: '' };
        };

        const put = async (action, changes, version) => {
            const body = new FormData();
            body.set('ExpectedVersion', String(version));
            body.set('FormAttemptId', editor.attemptId);
            Object.entries(changes).forEach(([field, value]) => body.set(formNames[field],
                field === 'uomConversions' ? JSON.stringify(value)
                    : field === 'storageConditionCodes' ? value.join(',') : String(value)));
            const token = offcanvas.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            body.set('__RequestVerificationToken', token);
            const response = await fetch(`${endpoint}/${encodeURIComponent(editor.id)}/${action}`, {
                method: 'PUT', credentials: 'same-origin',
                headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' },
                body
            });
            if (response.status === 401) {
                window.DtDefaults?.handleUnauthorized?.();
                const error = new Error('auth-refresh-in-progress');
                error.authHandled = true;
                throw error;
            }
            let payload = {};
            try { payload = await response.json(); } catch (error) { }
            if (!response.ok) {
                // The proxy says every refusal as a sentence; `unmapped` is a missing mapping (status only, no body logged).
                if (payload?.unmapped === true) console.warn('Stock behaviour: a refusal without a sentence mapping', response.status);
                const errors = payload?.errors || payload?.Errors;
                throw new Error((Array.isArray(errors) && errors.find(Boolean)) || words.SaveFailed);
            }
            const data = payload?.data ?? payload?.Data;
            const answered = valueOf(data, 'version');
            if ((payload?.isSuccessful ?? payload?.IsSuccessful) !== true
                || String(valueOf(data, 'recordId')).toLowerCase() !== editor.id.toLowerCase()
                || !Number.isSafeInteger(answered) || answered < version) {
                throw new Error(words.SaveFailed);
            }
            editor.version = answered;
            return answered;
        };

        // After a partial save: the detail again, the editor moved to its version and values; the GTIN typed stays typed.
        const refreshAfterPartialSave = async (id) => {
            try {
                const refreshed = await fetchDetail(id);
                render(refreshed);
                const sb = valueOf(refreshed, 'stockBehaviour');
                const current = valueOf(refreshed, versionField);
                if (!editor || editor.id !== id || !sb || !Number.isSafeInteger(current)) return;
                const typedGtin = input('gtin')?.value;
                editor.sb = sb;
                editor.version = current;
                fill(sb);
                if (input('gtin') && typedGtin !== undefined) input('gtin').value = typedGtin;
                onSaved?.(id);
            } catch (error) {
                // The refusal of the GTIN is what is said; a failed read leaves the editor at the version put() saw.
            }
        };

        const save = () => {
            if (!editor || busy) return;
            const { stock, gtin, error } = collect();
            if (error) {
                toast(error, 'error');
                return;
            }
            if (!Object.keys(stock).length && gtin === null) {
                toast(words.NoChanges, 'info');
                return;
            }
            const run = async () => {
                if (!editor || busy) return;
                busy = true;
                const button = offcanvas.querySelector('#btnSaveStockBehaviour');
                if (button) button.disabled = true;
                const id = editor.id;
                try {
                    let version = editor.version;
                    if (Object.keys(stock).length) version = await put('stock-behaviour', stock, version);
                    if (gtin !== null) {
                        try {
                            version = await put('gtin', { gtin }, version);
                        } catch (gtinFailure) {
                            // S5-FIX1: the stock part may be saved already — it is read back either way, so the
                            // section and the editor stand at MDM's version; the GTIN refusal keeps its own sentence.
                            if (Object.keys(stock).length && !gtinFailure?.authHandled) await refreshAfterPartialSave(id);
                            throw gtinFailure;
                        }
                    }
                    // Read back: the section shows what MDM now holds, never what was typed.
                    const refreshed = await fetchDetail(id);
                    render(refreshed);
                    bootstrap.Offcanvas.getOrCreateInstance(offcanvas).hide();
                    editor = null;
                    onSaved?.(id);
                    const current = valueOf(refreshed, versionField);
                    if (Number.isSafeInteger(current) && current >= version) toast(words.Saved, 'success');
                    else toast(words.StateChanged, 'warning');
                } catch (failure) {
                    if (!failure?.authHandled) toast(failure?.message || words.SaveFailed, 'error');
                } finally {
                    busy = false;
                    if (button) button.disabled = !editor || valueOf(editor.sb, 'editable') !== true;
                }
            };
            if (typeof window.showConfirm === 'function') {
                window.showConfirm(words.SaveConfirmation, run, { type: 'primary', confirmButtonText: words.Save });
            } else {
                run();
            }
        };

        const open = async (id) => {
            if (!id || busy) return;
            try {
                const [detail, lists] = await Promise.all([
                    fetchDetail(String(id)),
                    needsOptions ? loadOptions(words.OptionsUnavailable || '') : Promise.resolve(options)
                ]);
                options = lists;
                const sb = valueOf(detail, 'stockBehaviour');
                const version = valueOf(detail, versionField);
                if (!sb || !Number.isSafeInteger(version) || version < 0) throw new Error(words.SaveFailed);
                editor = { id: String(id), version, sb, attemptId: newAttemptId(), storage: [] };
                const subtitle = offcanvas.querySelector('#sb-form-subtitle');
                if (subtitle) subtitle.textContent = shown(valueOf(detail, 'canonicalCode'));
                fill(sb);
                render(detail);
                bootstrap.Offcanvas.getOrCreateInstance(offcanvas).show();
            } catch (error) {
                if (!error?.authHandled) toast(error?.message || words.SaveFailed, 'error');
            }
        };

        offcanvas.querySelector('#btnSaveStockBehaviour')?.addEventListener('click', save);
        offcanvas.querySelector('#sb-add-conversion')?.addEventListener('click', () =>
            input('uomConversions')?.appendChild(conversionRow(null, false)));
        const storageSelect = input('storageConditionCodes');
        if (storageSelect) {
            if (typeof window.jQuery === 'function') window.jQuery(storageSelect).on('change', enforceOneTemperature);
            else storageSelect.addEventListener('change', enforceOneTemperature);
        }
        input('uomConversions')?.addEventListener('click', (event) => {
            event.target.closest?.('[data-sb-remove]')?.closest('[data-sb-conversion-row]')?.remove();
        });
        document.getElementById('sb-edit')?.addEventListener('click', (event) => {
            const id = event.currentTarget?.dataset?.id;
            const preview = document.getElementById('offcanvasDetailsPreview');
            if (preview) bootstrap.Offcanvas.getInstance?.(preview)?.hide();
            open(id);
        });

        return { words, render, open, save };
    };

    return { create };
})();
