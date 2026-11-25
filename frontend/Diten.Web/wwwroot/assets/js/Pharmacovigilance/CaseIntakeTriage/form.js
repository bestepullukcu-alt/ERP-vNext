'use strict';

(function () {
    const form = document.getElementById('pvg-case-intake-form');
    if (!form) return;

    const L = window.PvgCaseIntakeTriageL10n || {};
    const t = key => L[key] || key;
    const alertEl = document.getElementById('pvg-form-alert');
    // The Save button sits in the page header, outside the form, and points at it through its form attribute.
    const submitButton = document.querySelector('button[type="submit"][form="pvg-case-intake-form"]') || form.querySelector('button[type="submit"]');
    const isEdit = (form.dataset.mode || 'create') === 'edit';
    const versionEl = document.getElementById('ExpectedVersion');
    const reportTypeEl = document.getElementById('ReportType');
    const eventCountryEl = document.getElementById('EventCountry');
    const reporterCountryEl = document.getElementById('ReporterCountry');
    const receivedAtEl = document.getElementById('ReceivedAtUtc');
    const awarenessAtEl = document.getElementById('AwarenessAtUtc');
    const selectionDirectoryEl = document.getElementById('ReporterSelectionDirectory');
    const selectionIdEl = document.getElementById('ReporterSelectionId');
    const removeLinkEl = document.getElementById('RemoveReporterLink');
    const reporterLinkEl = document.getElementById('pvg-reporter-link');
    const removeLinkButton = document.getElementById('pvg-remove-reporter-link');
    const removalNoteEl = document.getElementById('pvg-reporter-link-removal-note');
    const productLinesEl = document.getElementById('pvg-product-lines');
    const productLineTemplate = document.getElementById('pvg-product-line-template');
    const addProductLineButton = document.getElementById('pvg-add-product-line');
    const maxProductLines = 20;
    let versionReady = !isEdit;
    let storedDraft = null;
    const storedControls = new WeakMap();
    const editableFields = [
        'IntakeChannel', 'SourceType', 'SourceReference', 'ReceivedAtUtc', 'ReporterType', 'ReporterContactSummary',
        'PatientSubjectCode', 'EventOnsetDate', 'AdverseEventNarrative', 'Seriousness', 'IntakePriority', 'AwarenessAtUtc',
        'EventCountry', 'ReportType', 'ReporterNameOrInitials', 'ReporterProfession', 'ReporterCountry'
    ];
    let optionsReady = false;
    let options = null;
    let nextLineIndex = 0;
    const endpoint = '/Pharmacovigilance/CaseIntakeTriage/api';
    const ajaxHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const safeProxyUrl = path => {
        if (typeof path !== 'string' || !path.startsWith(`${endpoint}/`) || path.includes('://') || path.startsWith('//')) {
            throw new Error('Invalid same-origin PVG proxy endpoint.');
        }

        return path;
    };
    const canSave = () => optionsReady && versionReady;

    // Save and the option inputs stay off until the option lists are read (both modes). Edit reads the record only
    // after them: the record version read then travels back with the save (the service answers 409 when the record
    // moved on). Without a version there is nothing safe to save, so Save stays off.
    setSubmitting(false);
    setOptionInputsDisabled(true);
    updateAddProductLine();
    bindAwarenessDefault();
    bindProductLines();
    bindRemoveLink();
    loadOptions();

    form.addEventListener('submit', async event => {
        event.preventDefault();
        event.stopPropagation();
        form.classList.add('was-validated');
        const heldBack = holdBackEmptyFields();
        try {
            renumberProductLines();
            if (!form.checkValidity()) {
                showAlert(t('ReasonCode'));
                return;
            }

            if (!canSave()) return;
            const id = encodeURIComponent(form.dataset.intakeDraftId || '');
            const url = isEdit ? safeProxyUrl(`${endpoint}/update/${id}`) : safeProxyUrl(`${endpoint}/create`);
            setSubmitting(true);
            const result = await postForm(url, new FormData(form));
            setSubmitting(false);
            if (!result.ok) {
                showAlert(result.message);
                return;
            }

            const intakeDraftId = result.body?.intakeDraftId || result.body?.IntakeDraftId || result.body?.data?.intakeDraftId || id;
            window.location.assign(intakeDraftId
                ? `/Pharmacovigilance/CaseIntakeTriage/Details/${encodeURIComponent(intakeDraftId)}`
                : '/Pharmacovigilance/CaseIntakeTriage');
        } finally {
            heldBack.forEach(input => { input.disabled = false; });
        }
    });

    async function loadOptions() {
        try {
            const response = await fetch(safeProxyUrl(`${endpoint}/options`), {
                credentials: 'same-origin',
                headers: ajaxHeaders()
            });
            const body = await responseJson(response);
            if (!response.ok || !isOptionsAnswer(body)) {
                showAlert([401, 403].includes(response.status) ? safeMessage(body, response.status) : t('OptionsLoadFailed'));
                return;
            }

            options = body;
            drawOptions();
            optionsReady = true;
            setOptionInputsDisabled(false);
            updateAddProductLine();
            setSubmitting(false);
            if (isEdit) await loadVersion();
        } catch (error) {
            showAlert(error?.message === 'Invalid same-origin PVG proxy endpoint.' ? t('InvalidProxyEndpoint') : t('OptionsLoadFailed'));
        }
    }

    function isOptionsAnswer(body) {
        const isCodeList = value => Array.isArray(value) && value.every(code => typeof code === 'string' && code.length > 0);
        return isCodeList(body?.reportTypes)
            && isCodeList(body?.productRoles)
            && typeof body?.unknownCountry === 'string'
            && body.unknownCountry.length > 0;
    }

    function drawOptions() {
        options.reportTypes.forEach(code => addOption(reportTypeEl, code, reportTypeName(code)));
        addOption(eventCountryEl, options.unknownCountry, countryName(options.unknownCountry));
        addOption(reporterCountryEl, options.unknownCountry, countryName(options.unknownCountry));
    }

    // The displayed name is the page resource for the code; a code without one is shown as itself.
    function reportTypeName(code) {
        return L[`ReportType${code}`] || code;
    }

    function productRoleName(code) {
        return L[`ProductRole${code}`] || code;
    }

    function countryName(code) {
        return code === options?.unknownCountry ? (L.CountryUnknown || code) : code;
    }

    function addOption(select, code, name) {
        if (!select) return;
        const option = document.createElement('option');
        option.value = code;
        option.textContent = name;
        select.appendChild(option);
    }

    // A stored value the option list no longer holds stays on the record: it is offered as itself, not dropped.
    function selectStored(select, code, nameOf) {
        if (!select || code === null || code === undefined || code === '') return;
        if (![...select.options].some(option => option.value === code)) {
            addOption(select, code, nameOf(code));
        }

        select.value = code;
    }

    function setOptionInputsDisabled(disabled) {
        [reportTypeEl, eventCountryEl, reporterCountryEl].forEach(select => {
            if (select) select.disabled = disabled;
        });
    }

    function bindAwarenessDefault() {
        if (isEdit || !receivedAtEl || !awarenessAtEl) return;
        // The awareness moment defaults to the received moment until the user sets one of their own.
        let followsReceived = true;
        const stopFollowing = () => { followsReceived = false; };
        const follow = () => {
            if (followsReceived) awarenessAtEl.value = receivedAtEl.value;
        };
        awarenessAtEl.addEventListener('input', stopFollowing);
        awarenessAtEl.addEventListener('change', stopFollowing);
        receivedAtEl.addEventListener('input', follow);
        receivedAtEl.addEventListener('change', follow);
    }

    function bindProductLines() {
        if (!productLinesEl || !productLineTemplate || !addProductLineButton) return;
        addProductLineButton.addEventListener('click', () => {
            addProductLine();
        });
        productLinesEl.addEventListener('click', event => {
            const button = event.target.closest('[data-action="remove-product-line"]');
            if (!button) return;
            button.closest('[data-product-line]').remove();
            updateAddProductLine();
        });
    }

    function productLineRows() {
        return productLinesEl ? [...productLinesEl.querySelectorAll('[data-product-line]')] : [];
    }

    function addProductLine() {
        if (!optionsReady || !productLinesEl || !productLineTemplate || productLineRows().length >= maxProductLines) return null;
        const fragment = productLineTemplate.content.cloneNode(true);
        const row = fragment.querySelector('[data-product-line]');
        const index = nextLineIndex++;
        row.querySelectorAll('[name]').forEach(input => {
            input.name = input.name.replace('__index__', String(index));
        });
        const roleEl = lineField(row, 'ProductRole');
        options.productRoles.forEach(code => addOption(roleEl, code, productRoleName(code)));
        productLinesEl.appendChild(fragment);
        updateAddProductLine();
        return row;
    }

    function lineField(row, field) {
        return row.querySelector(`[name$=".${field}"]`);
    }

    function isEmptyLine(row) {
        return !lineField(row, 'ProductRole').value
            && !lineField(row, 'ProductNameAsReported').value.trim()
            && !lineField(row, 'LotNumber').value.trim();
    }

    function updateAddProductLine() {
        if (addProductLineButton) {
            addProductLineButton.disabled = !optionsReady || productLineRows().length >= maxProductLines;
        }
    }

    // Empty lines and empty hidden reporter fields do not travel, and do not count for validity. Only inputs that are
    // on now are turned off, and only those come back on: inputs that were already off stay off.
    function holdBackEmptyFields() {
        const candidates = productLineRows()
            .filter(isEmptyLine)
            .flatMap(row => [...row.querySelectorAll('[name]')]);
        [selectionDirectoryEl, selectionIdEl, removeLinkEl].forEach(input => {
            if (input && !input.value) candidates.push(input);
        });
        const heldBack = candidates.filter(input => !input.disabled);
        heldBack.forEach(input => { input.disabled = true; });
        return heldBack;
    }

    // The MVC binder stops at the first missing index, so the lines that travel are always numbered without a gap.
    function renumberProductLines() {
        productLineRows()
            .filter(row => !isEmptyLine(row))
            .forEach((row, index) => {
                row.querySelectorAll('[name]').forEach(input => {
                    input.name = input.name.replace(/^ProductLines\[[^\]]*\]/, `ProductLines[${index}]`);
                });
            });
    }

    function bindRemoveLink() {
        if (!removeLinkButton) return;
        // The confirmation is the Save click: leaving the page without saving leaves the link as it is.
        removeLinkButton.addEventListener('click', () => {
            if (selectionDirectoryEl) selectionDirectoryEl.value = '';
            if (selectionIdEl) selectionIdEl.value = '';
            if (removeLinkEl) removeLinkEl.value = 'true';
            reporterLinkEl?.classList.add('d-none');
            removalNoteEl?.classList.remove('d-none');
        });
    }

    function prefillFromDraft(draft) {
        storedDraft = draft;
        editableFields.forEach(field => {
            const input = document.getElementById(field);
            const value = draft[field.charAt(0).toLowerCase() + field.slice(1)];
            if (input?.tagName === 'SELECT') selectStored(input, value, code => L[`${field}${code}`] || code);
            else setFieldValue(input, ['ReceivedAtUtc', 'AwarenessAtUtc'].includes(field) ? toUtcInputValue(value) : value);
            rememberControl(input, value);
        });
        // Display delimiters and textarea line endings never replace the accepted stored sequence.
        const evidence = document.getElementById('EvidenceLinkReferencesRaw');
        setFieldValue(evidence, draft.evidenceLinkReferences.join('\n'));
        rememberControl(evidence, draft.evidenceLinkReferences.slice());
        draft.productLines.forEach(line => {
            const row = addProductLine();
            if (!row) return;
            selectStored(lineField(row, 'ProductRole'), line.productRole, productRoleName);
            ['ProductRole', 'ProductNameAsReported', 'LotNumber'].forEach(field => {
                const input = lineField(row, field);
                const value = line[field.charAt(0).toLowerCase() + field.slice(1)];
                if (field !== 'ProductRole') setFieldValue(input, value);
                rememberControl(input, value);
            });
        });
        if (['Crm', 'PersonDirectory'].includes(draft.reporterSource)) {
            setFieldValue(selectionDirectoryEl, draft.reporterSource);
            setFieldValue(selectionIdEl, draft.reporterSourceId);
            reporterLinkEl?.classList.remove('d-none');
        }
    }

    function rememberControl(input, value) {
        if (!input) return;
        // Capture AFTER the browser's input/textarea/date sanitization. A deliberate edit wins even when cleared.
        const state = { raw: value, displayed: input.value, changed: false };
        storedControls.set(input, state);
        const changed = () => { state.changed = true; };
        input.addEventListener('input', changed);
        input.addEventListener('change', changed);
    }

    function controlValue(input, fallback) {
        if (!input) return fallback;
        const state = storedControls.get(input);
        return state && !state.changed && input.value === state.displayed ? state.raw : input.value;
    }

    function updateValues() {
        const payload = {};
        editableFields.forEach(field => {
            const input = document.getElementById(field);
            const raw = storedDraft[field.charAt(0).toLowerCase() + field.slice(1)];
            let value = controlValue(input, raw);
            const state = input && storedControls.get(input);
            if (['ReceivedAtUtc', 'AwarenessAtUtc'].includes(field) && input
                && (!state || state.changed || input.value !== state.displayed)) {
                value = input.value ? `${input.value}${input.value.length === 16 ? ':00' : ''}Z` : null;
            }
            if (field === 'EventOnsetDate' && value === '') value = null;
            payload[field] = value;
        });
        payload.ProductLines = productLineRows().filter(row => !isEmptyLine(row)).map(row => ({
            ProductRole: controlValue(lineField(row, 'ProductRole'), null),
            ProductNameAsReported: controlValue(lineField(row, 'ProductNameAsReported'), null),
            LotNumber: controlValue(lineField(row, 'LotNumber'), null)
        }));
        const evidence = controlValue(document.getElementById('EvidenceLinkReferencesRaw'), storedDraft.evidenceLinkReferences);
        payload.EvidenceLinkReferences = Array.isArray(evidence) ? evidence
            : [...new Set(evidence.split(/[,\r\n]/).map(value => value.trim()).filter(Boolean))];
        payload.ExpectedVersion = Number(versionEl?.value);
        if (selectionDirectoryEl?.value) payload.ReporterSelectionDirectory = selectionDirectoryEl.value;
        if (selectionIdEl?.value) payload.ReporterSelectionId = selectionIdEl.value;
        if (removeLinkEl?.value) payload.RemoveReporterLink = removeLinkEl.value === 'true';
        return payload;
    }

    function validDay(value) {
        if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
        const [year, month, day] = value.split('-').map(Number);
        const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
        const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
        return year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= days[month - 1];
    }

    function validInstant(value) {
        if (typeof value !== 'string') return false;
        const parts = /^(\d{4}-\d{2}-\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d{1,7})?(Z|[+-](\d{2}):(\d{2}))$/.exec(value);
        if (!parts || !validDay(parts[1]) || Number(parts[2]) > 23 || Number(parts[3]) > 59 || Number(parts[4]) > 59) return false;
        if (parts[5] !== 'Z' && (Number(parts[6]) > 14 || Number(parts[7]) > 59 || (Number(parts[6]) === 14 && Number(parts[7]) !== 0))) return false;
        const parsed = new Date(value);
        // Validate the representable UTC range only; retain the original string/ticks for submission.
        return Number.isFinite(parsed.getTime()) && parsed.getUTCFullYear() >= 1 && parsed.getUTCFullYear() <= 9999;
    }

    function completeDetail(body) {
        const draft = body?.draft;
        const plain = value => value !== null && typeof value === 'object' && !Array.isArray(value)
            && Object.prototype.toString.call(value) === '[object Object]';
        const own = (value, key) => Object.prototype.hasOwnProperty.call(value, key);
        const text = value => typeof value === 'string' && value.trim().length > 0;
        const nullable = value => value === null || typeof value === 'string';
        const members = [
            'intakeDraftId', 'status', 'version', 'productLines', 'awarenessAtUtc', 'companyRecipientPersonId',
            'eventCountry', 'reportType', 'reporterSource', 'reporterSourceId', 'reporterCrmSearchState',
            'reporterNameOrInitials', 'reporterProfession', 'reporterCountry', 'intakeChannel', 'sourceType',
            'sourceReference', 'receivedAtUtc', 'reporterType', 'reporterContactSummary', 'patientSubjectCode',
            'eventOnsetDate', 'adverseEventNarrative', 'seriousness', 'intakePriority', 'triageOutcome', 'triageReason',
            'routeTargetQueue', 'evidenceLinkReferences'
        ];
        if (!plain(draft) || !members.every(key => own(draft, key))) return false;
        if (!text(draft.intakeDraftId) || draft.intakeDraftId !== form.dataset.intakeDraftId
            || !Number.isSafeInteger(draft.version) || draft.version <= 0 || draft.version !== versionOf(body)) return false;
        if (!['status', 'eventCountry', 'reportType', 'reporterSource', 'reporterCrmSearchState', 'intakeChannel', 'sourceType',
            'reporterType', 'adverseEventNarrative', 'seriousness', 'intakePriority'].every(key => text(draft[key]))) return false;
        if (!['sourceReference', 'reporterContactSummary', 'patientSubjectCode', 'companyRecipientPersonId', 'reporterSourceId',
            'reporterNameOrInitials', 'reporterProfession', 'reporterCountry', 'triageOutcome', 'triageReason', 'routeTargetQueue']
            .every(key => nullable(draft[key]))) return false;
        if (draft.eventOnsetDate !== null && !validDay(draft.eventOnsetDate)) return false;
        if (!validInstant(draft.receivedAtUtc) || !validInstant(draft.awarenessAtUtc)) return false;
        if (!Array.isArray(draft.evidenceLinkReferences) || !draft.evidenceLinkReferences.every(nullable)) return false;
        if (!Array.isArray(draft.productLines) || draft.productLines.length > maxProductLines || !draft.productLines.every(line =>
            plain(line) && ['productRole', 'globalProductId', 'productNameAsReported', 'presentationGskuId', 'lotNumber'].every(key => own(line, key))
            && text(line.productRole) && text(line.productNameAsReported)
            && ['globalProductId', 'presentationGskuId', 'lotNumber'].every(key => nullable(line[key])))) return false;
        return true;
    }

    function setFieldValue(input, value) {
        if (input) input.value = value ?? '';
    }

    // The API answers an offset moment; the form holds the same UTC moment with its seconds, never local time.
    function toUtcInputValue(value) {
        return new Date(value).toISOString().slice(0, 19);
    }

    async function loadVersion() {
        try {
            const response = await fetch(safeProxyUrl(`${endpoint}/detail/${encodeURIComponent(form.dataset.intakeDraftId || '')}`), {
                credentials: 'same-origin',
                headers: ajaxHeaders()
            });
            const body = await responseJson(response);
            const version = versionOf(body);
            if (!response.ok || isBlocked(body) || version === null || !completeDetail(body)) {
                showAlert(safeMessage(body, response.status));
                return;
            }
            prefillFromDraft(body?.draft);
            if (versionEl) versionEl.value = String(version);
            versionReady = true;
            setSubmitting(false);
        } catch (error) {
            showAlert(error?.message === 'Invalid same-origin PVG proxy endpoint.' ? t('InvalidProxyEndpoint') : t('ErrorOccurred'));
        }
    }

    function versionOf(body) {
        const items = body?.items || body?.Items || body?.data?.items || body?.Data?.Items || [];
        const item = Array.isArray(items) ? items[0] : null;
        const value = item?.version ?? item?.Version ?? body?.draft?.version ?? body?.Draft?.Version ?? body?.version ?? body?.Version;
        return Number.isInteger(Number(value)) && value !== null && value !== undefined && value !== '' ? Number(value) : null;
    }

    async function postForm(url, formData) {
        if (isEdit) formData.set('UpdateValuesJson', JSON.stringify(updateValues()));
        try {
            const response = await fetch(url, {
                method: 'POST',
                body: formData,
                headers: { ...ajaxHeaders(), ...antiForgeryHeader(form) },
                credentials: 'same-origin'
            });
            const body = await responseJson(response);
            if (!response.ok || isBlocked(body)) {
                return { ok: false, message: safeMessage(body, response.status) };
            }
            return { ok: true, body };
        } catch (error) {
            return { ok: false, message: error?.message === 'Invalid same-origin PVG proxy endpoint.' ? t('InvalidProxyEndpoint') : t('ErrorOccurred') };
        }
    }

    function antiForgeryHeader(scope) {
        const token = scope.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        return token ? { RequestVerificationToken: token } : {};
    }

    async function responseJson(response) {
        try {
            return await response.json();
        } catch (error) {
            return {};
        }
    }

    function isBlocked(body) {
        const outcome = body?.outcome || body?.Outcome || '';
        const statusCode = Number(body?.statusCode || body?.StatusCode || 0);
        return ['Blocked', 'Invalid'].includes(outcome) || [401, 403, 409].includes(statusCode);
    }

    function safeMessage(body, statusCode) {
        const status = Number(statusCode || body?.statusCode || body?.StatusCode || 0);
        if (status === 401) return t('SessionExpired');
        if (status === 403) return t('NotAuthorized');

        const reason = safeCode(body?.reasonCode || body?.ReasonCode || body?.reason_code || '');
        const validation = body?.validationReasonCodes || body?.ValidationReasonCodes || [];
        const codes = Array.isArray(validation) && validation.length
            ? validation.map(safeCode).filter(Boolean).join(', ')
            : reason;
        if (status === 409 && [reason, ...(Array.isArray(validation) ? validation.map(safeCode) : [])].includes('PVG_INTAKE_DRAFT_VERSION_CONFLICT')) {
            return t('VersionConflict');
        }
        if (status === 409 && [reason, ...(Array.isArray(validation) ? validation.map(safeCode) : [])].includes('PVG_INTAKE_DRAFT_NOT_EDITABLE')) {
            return t('DraftNotEditable');
        }
        if (status === 409) {
            return `${t('ControlledBlock')}: ${codes || reason || t('ReasonCode')}`;
        }

        return codes ? `${t('ControlledBlock')}: ${codes}` : t('ErrorOccurred');
    }

    function safeCode(value) {
        return String(value || '').replace(/[^A-Za-z0-9._-]/g, '').slice(0, 96);
    }

    function showAlert(message) {
        if (!alertEl) return;
        alertEl.textContent = message;
        alertEl.classList.remove('d-none');
    }

    function setSubmitting(isSubmitting) {
        if (!submitButton) return;
        submitButton.disabled = isSubmitting || !canSave();
        submitButton.setAttribute('aria-busy', isSubmitting ? 'true' : 'false');
    }
})();
