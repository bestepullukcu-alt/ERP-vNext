/**
 * Notification Templates - Create/Edit form (MOD-0027-FU02).
 * No-ViewModel: data loads and saves through the same-origin /Platform/NotificationTemplates/api proxy.
 * Preview renders unsaved editor content server-side and displays it in a fully sandboxed iframe.
 */
'use strict';

(function () {
    const apiBase = '/Platform/NotificationTemplates/api';
    const form = document.getElementById('notificationTemplateForm');
    if (!form) return;

    const mode = form.dataset.mode || 'create';
    const templateId = form.dataset.templateId || '';
    const scopeTenantId = form.dataset.scopeTenantId || '';
    const L = () => window.L10n || {};

    const errorSummary = document.getElementById('formErrorSummary');
    const scopeBanner = document.getElementById('scopeBanner');
    const variablesEditor = document.getElementById('variablesEditor');
    const variablesEmptyState = document.getElementById('variablesEmptyState');
    const rowTemplate = document.getElementById('variableRowTemplate');

    const unwrap = (payload) => payload?.data ?? payload?.Data ?? null;
    const errorsOf = (payload) => payload?.errors || payload?.Errors || [];

    const showSummary = (messages) => {
        if (!errorSummary) return;
        const list = Array.isArray(messages) ? messages : [messages];
        errorSummary.innerHTML = list.filter(Boolean).map((m) => `<div>${m}</div>`).join('');
        errorSummary.classList.toggle('d-none', list.filter(Boolean).length === 0);
    };
    const clearFieldErrors = () => {
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        form.querySelectorAll('.invalid-feedback').forEach((el) => { el.textContent = ''; });
    };
    const setFieldError = (fieldName, message) => {
        const feedback = form.querySelector(`[data-valmsg-for="${fieldName}"]`);
        if (feedback) {
            feedback.textContent = message;
            feedback.previousElementSibling?.classList?.add('is-invalid');
        }
    };
    const mapServerErrors = (messages) => {
        const fieldMap = [
            ['TemplateKey', /template\s*key/i],
            ['Locale', /locale/i],
            ['Channel', /channel/i],
            ['SubjectTemplate', /subject/i],
            ['BodyHtmlTemplate', /bodyhtml|html/i],
            ['BodyTextTemplate', /bodytext/i],
            ['Status', /status/i]
        ];
        const unmapped = [];
        (messages || []).forEach((message) => {
            const hit = fieldMap.find(([, pattern]) => pattern.test(message));
            if (hit) setFieldError(hit[0], message);
            else unmapped.push(message);
        });
        showSummary(unmapped);
    };

    const fillSelect = (select, options, selected) => {
        if (!select) return;
        select.innerHTML = '';
        options.forEach((item) => {
            if (!item?.value) return;
            const opt = document.createElement('option');
            opt.value = item.value;
            opt.textContent = item.name || item.code || item.value;
            select.appendChild(opt);
        });
        if (selected) select.value = selected;
    };
    const fetchLookup = async (key) => {
        const res = await fetch(`${apiBase}/lookups/${encodeURIComponent(key)}`, { credentials: 'same-origin' });
        if (!res.ok) throw new Error(`Lookup '${key}' failed (${res.status}).`);
        return unwrap(await res.json()) || [];
    };

    const addVariableRow = (variable) => {
        if (!rowTemplate || !variablesEditor) return;
        const fragment = rowTemplate.content.cloneNode(true);
        const row = fragment.querySelector('.variable-row');
        if (variable) {
            row.querySelector('.variable-name').value = variable.name || '';
            row.querySelector('.variable-type').value = variable.type || 'String';
            row.querySelector('.variable-required').checked = variable.isRequired !== false;
        }
        row.querySelector('.variable-remove')?.addEventListener('click', () => {
            row.remove();
            syncVariablesEmptyState();
        });
        variablesEditor.appendChild(fragment);
        syncVariablesEmptyState();
    };
    const syncVariablesEmptyState = () => {
        variablesEmptyState?.classList.toggle('d-none', variablesEditor.children.length > 0);
    };
    const collectVariables = () =>
        Array.from(variablesEditor.querySelectorAll('.variable-row'))
            .map((row) => ({
                name: row.querySelector('.variable-name')?.value?.trim() || '',
                type: row.querySelector('.variable-type')?.value || 'String',
                isRequired: !!row.querySelector('.variable-required')?.checked
            }))
            .filter((v) => v.name.length > 0);

    // --- Preview (sandboxed iframe, server-side render of the UNSAVED editor content) ---
    const previewSampleInputs = document.getElementById('previewSampleInputs');
    const previewError = document.getElementById('previewError');
    const previewResult = document.getElementById('previewResult');
    const previewSubject = document.getElementById('previewSubject');
    const previewFrame = document.getElementById('previewFrame');
    const previewText = document.getElementById('previewText');

    const syncSampleInputs = () => {
        if (!previewSampleInputs) return;
        const existing = {};
        previewSampleInputs.querySelectorAll('input[data-variable]').forEach((input) => {
            existing[input.dataset.variable] = input.value;
        });
        previewSampleInputs.innerHTML = '';
        collectVariables().forEach((variable) => {
            const wrapper = document.createElement('div');
            const label = document.createElement('label');
            label.className = 'form-label small mb-1';
            label.textContent = variable.name + (variable.isRequired ? ' *' : '');
            const input = document.createElement('input');
            input.type = 'text';
            input.className = 'form-control form-control-sm';
            input.dataset.variable = variable.name;
            input.value = existing[variable.name] || '';
            wrapper.appendChild(label);
            wrapper.appendChild(input);
            previewSampleInputs.appendChild(wrapper);
        });
    };
    const collectSampleVariables = () => {
        const samples = {};
        previewSampleInputs?.querySelectorAll('input[data-variable]').forEach((input) => {
            if (input.value !== '') samples[input.dataset.variable] = input.value;
        });
        return samples;
    };
    const renderPreview = async () => {
        previewError?.classList.add('d-none');
        syncSampleInputs();
        const payload = {
            subjectTemplate: document.getElementById('subjectTemplate')?.value || '',
            bodyHtmlTemplate: document.getElementById('bodyHtmlTemplate')?.value || null,
            bodyTextTemplate: document.getElementById('bodyTextTemplate')?.value || null,
            variables: collectVariables(),
            sampleVariables: collectSampleVariables()
        };
        try {
            const res = await fetch(`${apiBase}/templates/render-preview`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            const body = await res.json();
            if (!res.ok) {
                previewResult?.classList.add('d-none');
                if (previewError) {
                    previewError.textContent = errorsOf(body).join(' ') || L().ErrorOccurred || '';
                    previewError.classList.remove('d-none');
                }
                return;
            }
            const rendered = unwrap(body);
            if (previewSubject) previewSubject.textContent = rendered?.subject || '';
            if (previewFrame) previewFrame.srcdoc = rendered?.bodyHtml || '';
            if (previewText) {
                previewText.textContent = rendered?.bodyText || '';
                previewText.classList.toggle('d-none', !rendered?.bodyText);
            }
            previewResult?.classList.remove('d-none');
        } catch (error) {
            console.error('[NotificationTemplates Preview] Failed.', error);
            if (previewError) {
                previewError.textContent = L().ErrorOccurred || '';
                previewError.classList.remove('d-none');
            }
        }
    };

    const loadTemplate = async () => {
        if (mode !== 'edit' || !templateId) return;
        try {
            const res = await fetch(`${apiBase}/templates/${templateId}`, { credentials: 'same-origin' });
            const body = await res.json();
            if (!res.ok) {
                showSummary(errorsOf(body).join(' ') || L().ErrorOccurred);
                return;
            }
            const dto = unwrap(body);
            if (!dto) return;
            document.getElementById('templateKey').value = dto.templateKey || '';
            document.getElementById('templateLocale').value = dto.locale || '';
            document.getElementById('templateChannel').value = dto.channel || '';
            document.getElementById('templateStatus').value = dto.status || '';
            document.getElementById('semanticVersion').value = dto.semanticVersion || '';
            document.getElementById('subjectTemplate').value = dto.subjectTemplate || '';
            document.getElementById('bodyHtmlTemplate').value = dto.bodyHtmlTemplate || '';
            document.getElementById('bodyTextTemplate').value = dto.bodyTextTemplate || '';
            variablesEditor.innerHTML = '';
            (dto.variables || []).forEach(addVariableRow);
            syncSampleInputs();
        } catch (error) {
            console.error('[NotificationTemplates Form] Load failed.', error);
            showSummary(L().ErrorOccurred);
        }
    };

    const buildSaveUrl = () => {
        if (mode === 'edit') {
            return scopeTenantId
                ? { url: `${apiBase}/tenant/${scopeTenantId}/templates/${templateId}`, method: 'PUT' }
                : { url: `${apiBase}/templates/${templateId}`, method: 'PUT' };
        }
        return scopeTenantId
            ? { url: `${apiBase}/tenant/${scopeTenantId}/templates`, method: 'POST' }
            : { url: `${apiBase}/templates`, method: 'POST' };
    };

    const submitForm = async (event) => {
        event.preventDefault();
        clearFieldErrors();
        showSummary([]);
        const payload = {
            isPlatformDefault: !scopeTenantId,
            templateKey: document.getElementById('templateKey')?.value?.trim() || '',
            channel: document.getElementById('templateChannel')?.value || '',
            locale: document.getElementById('templateLocale')?.value || '',
            subjectTemplate: document.getElementById('subjectTemplate')?.value || '',
            bodyHtmlTemplate: document.getElementById('bodyHtmlTemplate')?.value || '',
            bodyTextTemplate: document.getElementById('bodyTextTemplate')?.value || null,
            variables: collectVariables(),
            status: document.getElementById('templateStatus')?.value || '',
            semanticVersion: document.getElementById('semanticVersion')?.value?.trim() || null
        };
        const { url, method } = buildSaveUrl();
        const saveButton = document.getElementById('btnSaveTemplate');
        saveButton?.setAttribute('disabled', 'disabled');
        try {
            const res = await fetch(url, {
                method: method,
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            const body = res.status === 204 ? null : await res.json().catch(() => null);
            if (res.ok) {
                window.showToast?.(L().RecordSaved || '', 'success');
                window.location.href = '/Platform/NotificationTemplates';
                return;
            }
            if (res.status === 409) {
                setFieldError('TemplateKey', errorsOf(body).join(' ') || L().ErrorOccurred);
                return;
            }
            mapServerErrors(errorsOf(body));
            if (!errorsOf(body).length) showSummary(L().ErrorOccurred);
        } catch (error) {
            console.error('[NotificationTemplates Form] Save failed.', error);
            showSummary(L().ErrorOccurred);
        } finally {
            saveButton?.removeAttribute('disabled');
        }
    };

    const init = async () => {
        if (scopeTenantId && scopeBanner) {
            scopeBanner.textContent = `${L().ScopeTenantOverrides || ''}: ${scopeTenantId}`;
            scopeBanner.classList.remove('d-none');
        }
        try {
            const [locales, channels, statuses] = await Promise.all([
                fetchLookup('locales'),
                fetchLookup('notification-channels'),
                fetchLookup('notification-template-statuses')
            ]);
            fillSelect(document.getElementById('templateLocale'), locales);
            fillSelect(document.getElementById('templateChannel'), channels);
            fillSelect(document.getElementById('templateStatus'), statuses);
        } catch (error) {
            // Controlled degraded state: selects stay empty; save will fail validation server-side.
            console.error('[NotificationTemplates Form] Lookup load failed.', error);
            showSummary(L().ErrorOccurred);
        }
        await loadTemplate();
        syncVariablesEmptyState();
        document.getElementById('btnAddVariable')?.addEventListener('click', () => {
            addVariableRow(null);
            syncSampleInputs();
        });
        document.getElementById('btnRenderPreview')?.addEventListener('click', renderPreview);
        form.addEventListener('submit', submitForm);
    };

    document.addEventListener('DOMContentLoaded', init);
})();
